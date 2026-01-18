using System.Text.Json;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.WorkerCritic;

public static class Program
{
    private const string WorkerCompletionSignal = "<worker>DONE</worker>";
    private const string CriticApprovalSignal = "<critic>APPROVED</critic>";
    private const string CriticRejectionSignal = "<critic>NEEDS_WORK</critic>";
    private const int DefaultMaxTurns = 10;

    public static async Task<int> Main(string[] args)
    {
        bool verbose = args.Contains("--verbose");
        bool minimal = args.Contains("--minimal");
        int maxTurnsPerTask = GetArgValue(args, "--max-turns", DefaultMaxTurns);
        int? maxTotalTurns = GetArgValueOrNull(args, "--max-total");

        Console.WriteLine("════════════════════════════════════════════════════════");
        Console.WriteLine("     Ralph Loop - Worker-Critic Validation Demo");
        Console.WriteLine("════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine($"Max worker turns per task: {maxTurnsPerTask}");
        if (maxTotalTurns.HasValue)
        {
            Console.WriteLine($"Max total turns: {maxTotalTurns.Value}");
        }

        if (minimal)
        {
            Console.WriteLine("Minimal mode: CLAUDE.md files will not be loaded");
        }

        Console.WriteLine();

        // Setup directories - use FULL ABSOLUTE PATHS
        string baseDir = Directory.GetCurrentDirectory();
        string outputDir = Path.GetFullPath(Path.Combine(baseDir, "output"));
        string progressFile = Path.Combine(outputDir, "progress.txt");
        string tasksFile = Path.Combine(outputDir, "tasks.json");

        // Clear and recreate output directory
        ClearDirectory(outputDir);
        Console.WriteLine($"Output directory: {outputDir}");
        Console.WriteLine();

        // Initialize task list with states
        List<RalphTask> tasks = CreateTasks(outputDir);

        // Save initial task state
        await SaveTasksAsync(tasksFile, tasks);

        // Initialize progress file
        await InitializeProgressFileAsync(progressFile, tasks);

        Console.WriteLine($"Loaded {tasks.Count} tasks:");
        foreach (RalphTask task in tasks)
        {
            Console.WriteLine($"  [{task.Id}] {task.Title} - {task.Status}");
        }

        Console.WriteLine();
        Console.WriteLine("Starting ralph loop...");
        Console.WriteLine("────────────────────────────────────────────────────────");
        Console.WriteLine();

        // Track overall stats
        double totalCost = 0;
        int totalTurns = 0;

        // Execute each task sequentially (with retry on failure)
        foreach (RalphTask task in tasks)
        {
            int sessionAttempt = 0;

            // Retry loop: keep trying this task until success or global budget exhausted
            while (true)
            {
                sessionAttempt++;

                // Check if we've exceeded total turn budget
                if (maxTotalTurns.HasValue && totalTurns >= maxTotalTurns.Value)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Total turn budget exhausted ({totalTurns}/{maxTotalTurns.Value})");
                    Console.ResetColor();
                    task.Status = TaskStatus.Failed;
                    await SaveTasksAsync(tasksFile, tasks);
                    break;
                }

                // Calculate remaining turns for this attempt
                int remainingBudget = maxTotalTurns.HasValue
                    ? maxTotalTurns.Value - totalTurns
                    : int.MaxValue;
                int effectiveMaxTurns = Math.Min(maxTurnsPerTask, remainingBudget);

                if (effectiveMaxTurns <= 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("No turns remaining for task retry");
                    Console.ResetColor();
                    task.Status = TaskStatus.Failed;
                    await SaveTasksAsync(tasksFile, tasks);
                    break;
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("╔══════════════════════════════════════════════════════╗");
                Console.WriteLine($"║ Task: {task.Title,-46} ║");
                if (sessionAttempt > 1)
                {
                    Console.WriteLine($"║ Session Attempt: {sessionAttempt,-39} ║");
                }

                Console.WriteLine("╚══════════════════════════════════════════════════════╝");
                Console.ResetColor();

                if (maxTotalTurns.HasValue)
                {
                    Console.WriteLine(
                        $"  Turn budget: {effectiveMaxTurns} (used {totalTurns}/{maxTotalTurns.Value} total)");
                }

                Console.WriteLine();

                // Mark task as in_progress
                task.Status = TaskStatus.InProgress;
                await SaveTasksAsync(tasksFile, tasks);

                // Run the worker-critic loop for this task (new session each attempt)
                TaskResult result = await RunWorkerCriticLoopAsync(
                    task,
                    outputDir,
                    progressFile,
                    effectiveMaxTurns,
                    verbose,
                    minimal);

                totalCost += result.TotalCost;
                totalTurns += result.Iterations;

                // Update task status based on result
                if (result.Completed)
                {
                    task.Status = TaskStatus.Completed;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(
                        $"[COMPLETED] {task.Title} in {result.Iterations} turn(s) (session {sessionAttempt})");
                    Console.ResetColor();
                    await SaveTasksAsync(tasksFile, tasks);

                    // Summarize progress to reduce context for future tasks
                    double summaryCost = await SummarizeProgressAsync(
                        progressFile,
                        task,
                        tasks,
                        outputDir,
                        verbose,
                        minimal);
                    totalCost += summaryCost;

                    Console.WriteLine();
                    break; // Move to next task
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[SESSION FAILED] {task.Title} after {result.Iterations} turn(s)");
                if (!string.IsNullOrEmpty(result.Error))
                {
                    Console.WriteLine($"  Error: {result.Error}");
                }

                Console.ResetColor();

                // Check if we can retry with a new session
                bool canRetry = !maxTotalTurns.HasValue || totalTurns < maxTotalTurns.Value;

                if (canRetry)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("  Starting new session to retry task...");
                    Console.ResetColor();
                    Console.WriteLine();

                    // Append retry marker to progress file
                    await AppendRetryMarkerAsync(progressFile, task, sessionAttempt);

                    await Task.Delay(1000); // Brief pause before retry
                    // Continue to next iteration of while loop (new session)
                }
                else
                {
                    // No budget left, mark as failed and stop
                    task.Status = TaskStatus.Failed;
                    await SaveTasksAsync(tasksFile, tasks);
                    Console.WriteLine();
                    break;
                }
            }

            // If this task ultimately failed, mark remaining tasks as not attempted and stop
            if (task.Status == TaskStatus.Failed)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Stopping: task failed after exhausting retry budget.");
                Console.ResetColor();
                break;
            }
        }

        // Final report
        PrintFinalReport(tasks, totalCost, totalTurns, outputDir);

        bool allSuccess = tasks.All(t => t.Status == TaskStatus.Completed);
        return allSuccess ? 0 : 1;
    }

    /// <summary>
    ///     Runs the worker-critic loop for a single task.
    ///     Worker session persists across turns; critic validates completion.
    /// </summary>
    private static async Task<TaskResult> RunWorkerCriticLoopAsync(
        RalphTask currentTask,
        string outputDir,
        string progressFile,
        int maxTurns,
        bool verbose,
        bool minimal)
    {
        TaskResult result = new()
        {
            TaskId = currentTask.Id
        };

        // Configure the worker agent - session persists across turns
        ClaudeAgentOptions workerOptions = new()
        {
            WorkingDirectory = outputDir,
            PermissionMode = PermissionMode.BypassPermissions,
            Model = "haiku",
            MaxTurns = 20, // Allow enough tool turns within a single worker turn
            SettingSources = minimal ? [] : [SettingSource.Project, SettingSource.User],
            AllowedTools = ["Read", "Write", "Edit", "Glob"],
            SystemPrompt = $"""
                            You are a web development assistant working on building a portfolio webpage.

                            ## Important Instructions

                            1. ALWAYS read progress.txt first to understand what has been done
                            2. ALWAYS read existing files before modifying them
                            3. Work on the current task until it meets ALL acceptance criteria
                            4. When you believe the task is COMPLETE and meets all criteria, output EXACTLY:
                               {WorkerCompletionSignal}
                            5. If you receive feedback from a critic, address ALL issues before signaling completion again
                            6. Do NOT output the completion signal unless you believe the task is truly done

                            ## Output Format

                            When signaling completion, provide a SUCCINCT summary (2-4 sentences) of:
                            - What you created or modified
                            - Key decisions made
                            - How each acceptance criterion was satisfied

                            Keep your completion message brief - it will be logged to progress.txt for future reference.
                            Do NOT include full file contents in your summary.

                            ## Working Directory
                            All files should be in: {outputDir}
                            """
        };

        try
        {
            await using ClaudeAgentClient workerClient = new(workerOptions);
            await using ClaudeAgentSession workerSession = await workerClient.CreateSessionAsync();

            // Track cumulative context size
            int runningContext = 0;

            // WARMUP: Send a simple prompt to establish baseline context
            await workerSession.SendAsync("What's today's date?");
            await foreach (Message warmupMsg in workerSession.ReceiveResponseAsync())
            {
                if (warmupMsg is ResultMessage { Usage: not null } warmupResult)
                {
                    var u = warmupResult.Usage;
                    runningContext = (u.CacheCreationInputTokens ?? 0)
                                     + (u.CacheReadInputTokens ?? 0)
                                     + u.InputTokens
                                     + u.OutputTokens;
                }
            }

            // Build and send the initial worker prompt
            string initialPrompt = BuildWorkerPrompt(currentTask, outputDir, progressFile);

            if (verbose)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("  [Worker Prompt]");
                Console.WriteLine("  " +
                                  (initialPrompt.Length > 500 ? initialPrompt[..500] + "..." : initialPrompt).Replace(
                                      "\n", "\n  "));
                Console.ResetColor();
            }

            await workerSession.SendAsync(initialPrompt);

            // Worker-Critic loop
            for (int turn = 1; turn <= maxTurns; turn++)
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"  ── Worker Turn {turn}/{maxTurns} ──");
                Console.ResetColor();

                // Collect worker response
                IterationResult workerResult = new()
                {
                    ContextBefore = runningContext
                };

                await foreach (Message message in workerSession.ReceiveResponseAsync())
                {
                    switch (message)
                    {
                        case AssistantMessage assistant:
                            foreach (ContentBlock block in assistant.MessageContent.Content)
                            {
                                if (block is TextBlock text)
                                {
                                    workerResult.Output += text.Text;

                                    if (verbose)
                                    {
                                        Console.ForegroundColor = ConsoleColor.Gray;
                                        Console.Write(text.Text);
                                        Console.ResetColor();
                                    }
                                }
                                else if (block is ToolUseBlock toolUse)
                                {
                                    if (verbose)
                                    {
                                        Console.ForegroundColor = ConsoleColor.Yellow;
                                        Console.WriteLine($"  [Tool: {toolUse.Name}]");
                                        Console.ResetColor();
                                    }
                                }
                            }

                            break;

                        case ResultMessage resultMsg:
                            workerResult.Cost = resultMsg.TotalCostUsd ?? 0;
                            workerResult.IsError = resultMsg.SubtypeEnum == ResultMessageSubtype.Error;
                            if (workerResult.IsError)
                            {
                                workerResult.Error = resultMsg.Result ?? "Unknown error";
                            }

                            if (resultMsg.Usage is not null)
                            {
                                var u = resultMsg.Usage;
                                workerResult.InputTokens = u.InputTokens;
                                workerResult.OutputTokens = u.OutputTokens;
                                runningContext += (u.CacheCreationInputTokens ?? 0)
                                                  + u.InputTokens
                                                  + u.OutputTokens;
                                workerResult.ContextTokens = runningContext;
                            }

                            break;
                    }
                }

                if (verbose)
                {
                    Console.WriteLine();
                }

                result.Iterations++;
                result.TotalCost += workerResult.Cost;

                // Display context stats
                string beforeDisplay = FormatContextSize(workerResult.ContextBefore);
                string afterDisplay = FormatContextSize(workerResult.ContextTokens);
                int delta = workerResult.ContextTokens - workerResult.ContextBefore;
                string deltaDisplay = delta > 0 ? $"+{FormatContextSize(delta)}" : FormatContextSize(delta);
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine($"  Context: {beforeDisplay} → {afterDisplay} ({deltaDisplay})");
                Console.ResetColor();

                // Check for worker error
                if (workerResult.IsError)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  Worker Error: {workerResult.Error}");
                    Console.ResetColor();
                    await AppendProgressAsync(progressFile, currentTask, turn, workerResult, null);
                    continue; // Give worker another chance
                }

                // Check if worker signals completion
                if (workerResult.Output.Contains(WorkerCompletionSignal))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("  Worker signals completion. Running critic validation...");
                    Console.ResetColor();

                    // Run critic to validate
                    CriticResult criticResult = await RunCriticAsync(
                        currentTask,
                        workerResult.Output,
                        outputDir,
                        verbose,
                        minimal);

                    result.TotalCost += criticResult.Cost;

                    // Log progress with critic feedback
                    await AppendProgressAsync(progressFile, currentTask, turn, workerResult, criticResult);

                    if (criticResult.Approved)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("  Critic APPROVED! Task complete.");
                        Console.ResetColor();
                        result.Completed = true;
                        break;
                    }

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  Critic found issues:");
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"  {criticResult.Feedback.Replace("\n", "\n  ")}");
                    Console.ResetColor();

                    if (turn < maxTurns)
                    {
                        // Send full task context + critic feedback back to worker
                        string feedbackPrompt = BuildWorkerFeedbackPrompt(
                            currentTask,
                            outputDir,
                            progressFile,
                            criticResult.Feedback);

                        if (verbose)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.WriteLine("  [Sending task + critic feedback to worker]");
                            Console.ResetColor();
                        }

                        await workerSession.SendAsync(feedbackPrompt);
                    }
                }
                else
                {
                    // Worker didn't signal completion yet
                    await AppendProgressAsync(progressFile, currentTask, turn, workerResult, null);

                    if (turn < maxTurns)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine("  Worker still working. Continuing...");
                        Console.ResetColor();

                        // Prompt worker to continue
                        await workerSession.SendAsync($"""
                                                       Please continue working on the task. When complete, signal with:
                                                       {WorkerCompletionSignal}
                                                       """);
                    }
                }
            }

            if (!result.Completed && string.IsNullOrEmpty(result.Error))
            {
                result.Error = $"Max turns ({maxTurns}) exhausted without critic approval";
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    ///     Runs the critic as a one-shot validation of the worker's output.
    /// </summary>
    private static async Task<CriticResult> RunCriticAsync(
        RalphTask task,
        string workerOutput,
        string outputDir,
        bool verbose,
        bool minimal)
    {
        CriticResult result = new();

        ClaudeAgentOptions criticOptions = new()
        {
            WorkingDirectory = outputDir,
            PermissionMode = PermissionMode.BypassPermissions,
            Model = "haiku",
            MaxTurns = 5, // Critic just needs to read files and validate
            SettingSources = minimal ? [] : [SettingSource.Project, SettingSource.User],
            AllowedTools = ["Read", "Glob"], // Critic only reads, doesn't modify
            SystemPrompt = """
                           You are a strict quality assurance reviewer validating work against acceptance criteria.

                           ## Your Role

                           1. Review the worker's output and the actual files created
                           2. Check EACH acceptance criterion carefully
                           3. If ALL criteria are met, approve the work
                           4. If ANY criterion is not met, explain what's missing or wrong

                           ## Output Format

                           Keep your feedback SUCCINCT and ACTIONABLE:
                           - List only the specific issues that need fixing
                           - Use bullet points for multiple issues
                           - Do NOT include file contents or lengthy explanations
                           - Focus on WHAT is wrong, not HOW to fix it (worker knows how)

                           Your feedback will be logged and sent back to the worker, so be clear but brief.

                           ## Important

                           - Be thorough but fair
                           - Check the actual files, not just the worker's claims
                           - One missing criterion = rejection
                           """
        };

        string criticPrompt = BuildCriticPrompt(task, workerOutput, outputDir);

        if (verbose)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  [Critic Prompt]");
            Console.WriteLine("  " +
                              (criticPrompt.Length > 300 ? criticPrompt[..300] + "..." : criticPrompt).Replace("\n",
                                  "\n  "));
            Console.ResetColor();
        }

        try
        {
            await using ClaudeAgentClient criticClient = new(criticOptions);
            await using ClaudeAgentSession criticSession = await criticClient.CreateSessionAsync();

            await criticSession.SendAsync(criticPrompt);

            string fullOutput = "";

            await foreach (Message message in criticSession.ReceiveResponseAsync())
            {
                switch (message)
                {
                    case AssistantMessage assistant:
                        foreach (ContentBlock block in assistant.MessageContent.Content)
                        {
                            if (block is TextBlock text)
                            {
                                fullOutput += text.Text;

                                if (verbose)
                                {
                                    Console.ForegroundColor = ConsoleColor.Cyan;
                                    Console.Write(text.Text);
                                    Console.ResetColor();
                                }
                            }
                        }

                        break;

                    case ResultMessage resultMsg:
                        result.Cost = resultMsg.TotalCostUsd ?? 0;
                        if (resultMsg.Usage is not null)
                        {
                            result.InputTokens = resultMsg.Usage.InputTokens;
                            result.OutputTokens = resultMsg.Usage.OutputTokens;
                        }

                        break;
                }
            }

            if (verbose)
            {
                Console.WriteLine();
            }

            // Parse critic's decision
            if (fullOutput.Contains(CriticApprovalSignal))
            {
                result.Approved = true;
                result.Feedback = "All acceptance criteria met.";
            }
            else if (fullOutput.Contains(CriticRejectionSignal))
            {
                result.Approved = false;
                // Extract feedback (everything after the rejection signal or the whole output)
                int idx = fullOutput.IndexOf(CriticRejectionSignal, StringComparison.Ordinal);
                result.Feedback = idx >= 0
                    ? fullOutput[(idx + CriticRejectionSignal.Length)..].Trim()
                    : fullOutput;
            }
            else
            {
                // If no explicit signal, assume rejection and use output as feedback
                result.Approved = false;
                result.Feedback = fullOutput;
            }
        }
        catch (Exception ex)
        {
            result.Approved = false;
            result.Feedback = $"Critic error: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    ///     Builds the initial prompt for the worker.
    /// </summary>
    private static string BuildWorkerPrompt(RalphTask task, string outputDir, string progressFile)
    {
        return $"""
                ## Your Task

                1. First, read the progress log at `progress.txt` to understand what has been done
                2. Read any existing files (like portfolio.html) to see the current state
                3. Work on the current task: **{task.Title}**
                4. When complete, signal that you're done

                ## Current Task Details

                **ID:** {task.Id}
                **Title:** {task.Title}
                **Description:** {task.Description}

                ### Instructions

                {task.Instructions}

                ### Acceptance Criteria

                {string.Join("\n", task.AcceptanceCriteria.Select(c => $"- {c}"))}

                ## Important Files

                - Progress log: {progressFile}
                - Output directory: {outputDir}

                ## Completion

                When ALL acceptance criteria are met, output EXACTLY this signal on its own line:
                {WorkerCompletionSignal}

                A critic will then review your work. If they find issues, you'll receive feedback to address.
                """;
    }

    /// <summary>
    ///     Builds the prompt for the critic to validate worker output.
    /// </summary>
    private static string BuildCriticPrompt(RalphTask task, string workerOutput, string outputDir)
    {
        return $"""
                ## Review Request

                A worker has completed (or attempted) the following task. Please validate their work.

                ## Task Details

                **ID:** {task.Id}
                **Title:** {task.Title}
                **Description:** {task.Description}

                ### Acceptance Criteria (ALL must be met)

                {string.Join("\n", task.AcceptanceCriteria.Select(c => $"- [ ] {c}"))}

                ## Worker's Summary

                {workerOutput}

                ## Your Instructions

                1. Read the actual output files in: {outputDir}
                2. Check EACH acceptance criterion against the actual files
                3. Make your decision:

                If ALL criteria are satisfied, output EXACTLY:
                {CriticApprovalSignal}

                If ANY criterion is NOT satisfied, output EXACTLY:
                {CriticRejectionSignal}

                Then explain what is missing or incorrect in detail so the worker can fix it.

                ## Important

                - Check the actual files, not just what the worker claims
                - Be specific about what's wrong
                - One missing criterion = rejection
                """;
    }

    /// <summary>
    ///     Builds the prompt for the worker after critic rejection.
    ///     Includes full task context plus critic feedback.
    /// </summary>
    private static string BuildWorkerFeedbackPrompt(
        RalphTask task,
        string outputDir,
        string progressFile,
        string criticFeedback)
    {
        return $"""
                ## Critic Review - Issues Found

                The critic reviewed your work and found issues that need to be addressed:

                ---
                {criticFeedback}
                ---

                ## Task Requirements (for reference)

                **ID:** {task.Id}
                **Title:** {task.Title}
                **Description:** {task.Description}

                ### Instructions

                {task.Instructions}

                ### Acceptance Criteria (ALL must be met)

                {string.Join("\n", task.AcceptanceCriteria.Select(c => $"- {c}"))}

                ## Important Files

                - Progress log: {progressFile}
                - Output directory: {outputDir}

                ## Your Action Required

                1. Read the existing files to see current state
                2. Address ALL the issues mentioned by the critic above
                3. Ensure ALL acceptance criteria are satisfied
                4. When complete, signal with:
                   {WorkerCompletionSignal}

                The critic will review your work again. Make sure all issues are fully resolved.
                """;
    }

    private static List<RalphTask> CreateTasks(string outputDir)
    {
        return
        [
            new RalphTask
            {
                Id = "TASK-001",
                Title = "Create Basic HTML Structure",
                Description = "Create a basic portfolio webpage with semantic HTML structure",
                Instructions = $"""
                                Create a file named 'portfolio.html' in: {outputDir}

                                Requirements:
                                - Use semantic HTML5 elements (header, nav, main, section, footer)
                                - Include a navigation bar with links: Home, About, Projects, Contact
                                - Create a hero section with name "Alex Developer" and tagline "Full Stack Developer"
                                - Add an About section with 2-3 paragraphs of placeholder bio text
                                - Add a Projects section with 3 placeholder project cards (divs with titles)
                                - Add a Contact section with a placeholder email link
                                - Include a footer with copyright text
                                - Add proper meta tags (charset, viewport)
                                - Use id attributes on sections for navigation (home, about, projects, contact)

                                Output ONLY the HTML file. Do not add any CSS or JavaScript yet.
                                """,
                AcceptanceCriteria =
                [
                    "File portfolio.html exists",
                    "Uses semantic HTML5 elements (header, nav, main, section, footer)",
                    "Has navigation with links to Home, About, Projects, Contact",
                    "Has hero, about, projects, contact sections with id attributes",
                    "Valid HTML structure with doctype, charset, viewport meta tags"
                ],
                Status = TaskStatus.Pending
            },
            new RalphTask
            {
                Id = "TASK-002",
                Title = "Add CSS Styling",
                Description = "Style the portfolio page with modern CSS",
                Instructions = $"""
                                Update the portfolio.html file in: {outputDir}

                                Requirements:
                                - Add embedded <style> in the <head> section (no external files)
                                - Use CSS custom properties for colors: --primary: #3b82f6, --dark: #1e293b, --light: #f8fafc
                                - Style navigation: fixed at top, dark background, horizontal links with hover effects
                                - Style hero section: full viewport height, centered text, gradient background
                                - Style about section: max-width container, readable typography
                                - Style project cards: CSS grid (3 columns), shadows, border-radius, hover transform
                                - Style contact section and footer
                                - Add responsive media query for screens under 768px (stack to single column)
                                - Add smooth transitions on interactive elements

                                Read the existing HTML first, then add CSS while keeping HTML intact.
                                """,
                AcceptanceCriteria =
                [
                    "CSS is embedded in a style tag in the head",
                    "Uses CSS custom properties (--primary, --dark, --light)",
                    "Navigation is fixed position with styled links",
                    "Project cards use CSS grid layout",
                    "Has media query for responsive design under 768px",
                    "Transitions are applied to interactive elements"
                ],
                Status = TaskStatus.Pending
            },
            new RalphTask
            {
                Id = "TASK-003",
                Title = "Add Interactive JavaScript",
                Description = "Add interactivity with vanilla JavaScript",
                Instructions = $"""
                                Update the portfolio.html file in: {outputDir}

                                Requirements:
                                - Add embedded <script> at the end of body (no external files)
                                - Implement smooth scrolling when clicking navigation links
                                - Add IntersectionObserver to fade in sections as they scroll into view
                                - Add a "scroll to top" button that appears after scrolling down 300px
                                - Highlight the current section in navigation based on scroll position
                                - Add a simple typing effect on the hero tagline

                                Read the existing HTML/CSS first, then add JavaScript while keeping everything intact.
                                Use modern vanilla JS (no jQuery). Use addEventListener, not inline handlers.
                                """,
                AcceptanceCriteria =
                [
                    "JavaScript is embedded in a script tag at end of body",
                    "Smooth scrolling works when clicking nav links",
                    "Sections fade in using IntersectionObserver",
                    "Scroll-to-top button appears after scrolling 300px",
                    "Current section is highlighted in navigation",
                    "Uses modern vanilla JavaScript with addEventListener"
                ],
                Status = TaskStatus.Pending
            }
        ];
    }

    private static async Task InitializeProgressFileAsync(string progressFile, List<RalphTask> tasks)
    {
        string content = $"""
                          # Ralph Loop Progress Log (Worker-Critic Pattern)

                          Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

                          ## Tasks Overview

                          {string.Join("\n", tasks.Select(t => $"- [{t.Id}] {t.Title} - {t.Status}"))}

                          ## Turn Log

                          (Entries will be appended below as worker turns complete)

                          ---

                          """;

        await File.WriteAllTextAsync(progressFile, content);
    }

    private static async Task AppendProgressAsync(
        string progressFile,
        RalphTask task,
        int turn,
        IterationResult workerResult,
        CriticResult? criticResult)
    {
        bool workerSignaledDone = workerResult.Output.Contains(WorkerCompletionSignal);

        string criticSection = "";
        if (criticResult is not null)
        {
            string criticStatus = criticResult.Approved ? "APPROVED" : "NEEDS_WORK";

            criticSection = $"""

                             ### Critic Review
                             **Decision:** {criticStatus}
                             **Critic Cost:** ${criticResult.Cost:F4}
                             **Feedback:** {criticResult.Feedback}
                             """;
        }

        string entry = $"""

                        ## [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {task.Id} - Turn {turn}

                        **Status:** {(workerResult.IsError ? "Error" : "Completed turn")}
                        **Worker Cost:** ${workerResult.Cost:F4}
                        **Worker Signaled Done:** {(workerSignaledDone ? "YES" : "NO")}

                        **Worker Output:**
                        {workerResult.Output}
                        {criticSection}

                        ---

                        """;

        await File.AppendAllTextAsync(progressFile, entry);
    }

    private static async Task AppendRetryMarkerAsync(
        string progressFile,
        RalphTask task,
        int failedSessionNumber)
    {
        string entry = $"""

                        ## [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {task.Id} - SESSION {failedSessionNumber} FAILED

                        **Action:** Starting new session (attempt {failedSessionNumber + 1}) to retry task
                        **Reason:** Previous session exhausted max turns without critic approval

                        The new session should read this progress log to understand what was attempted before.

                        ---

                        """;

        await File.AppendAllTextAsync(progressFile, entry);
    }

    /// <summary>
    ///     Summarizes the progress file after a task completes to reduce context size.
    ///     Keeps recent entries detailed, compresses older history.
    /// </summary>
    private static async Task<double> SummarizeProgressAsync(
        string progressFile,
        RalphTask completedTask,
        List<RalphTask> allTasks,
        string outputDir,
        bool verbose,
        bool minimal)
    {
        string currentProgress = await File.ReadAllTextAsync(progressFile);

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  Summarizing progress log ({currentProgress.Length:N0} chars)...");
        Console.ResetColor();

        ClaudeAgentOptions summarizerOptions = new()
        {
            WorkingDirectory = outputDir,
            PermissionMode = PermissionMode.BypassPermissions,
            Model = "haiku",
            MaxTurns = 1, // One-shot, no tools needed
            SettingSources = minimal ? [] : [SettingSource.Project, SettingSource.User],
            AllowedTools = [], // No tools - just text processing
            SystemPrompt = """
                           You are a progress log summarizer. Your job is to compress a progress log while preserving essential information for future tasks.

                           ## Rules

                           1. ALWAYS preserve the header section (# Ralph Loop Progress Log, Generated date, Tasks Overview)
                           2. For COMPLETED tasks: Write a brief 2-3 sentence summary of what was accomplished and key decisions
                           3. For the MOST RECENT task section: Keep more detail as it provides context for the next task
                           4. Remove redundant information, verbose outputs, and repetitive entries
                           5. Keep all critic feedback summaries - they contain important learnings
                           6. Preserve session failure markers - they show what approaches didn't work
                           7. Output ONLY the summarized progress log - no commentary

                           ## Format

                           Output a valid progress.txt that a new worker can read to understand:
                           - What tasks are completed and what was done
                           - What the current task accomplished (if just completed)
                           - Any important patterns or issues discovered
                           """
        };

        string summaryPrompt = $"""
                                Please summarize this progress log. Task "{completedTask.Title}" ({completedTask.Id}) just completed successfully.

                                Current progress log:
                                ---
                                {currentProgress}
                                ---

                                Tasks status:
                                {string.Join("\n", allTasks.Select(t => $"- [{t.Id}] {t.Title}: {t.Status}"))}

                                Output the summarized progress log:
                                """;

        double cost = 0;

        try
        {
            await using ClaudeAgentClient summarizerClient = new(summarizerOptions);
            await using ClaudeAgentSession summarizerSession = await summarizerClient.CreateSessionAsync();

            await summarizerSession.SendAsync(summaryPrompt);

            string summarizedContent = "";

            await foreach (Message message in summarizerSession.ReceiveResponseAsync())
            {
                switch (message)
                {
                    case AssistantMessage assistant:
                        foreach (ContentBlock block in assistant.MessageContent.Content)
                        {
                            if (block is TextBlock text)
                            {
                                summarizedContent += text.Text;
                            }
                        }

                        break;

                    case ResultMessage resultMsg:
                        cost = resultMsg.TotalCostUsd ?? 0;
                        break;
                }
            }

            // Only replace if we got valid content
            if (!string.IsNullOrWhiteSpace(summarizedContent) && summarizedContent.Contains("# Ralph Loop"))
            {
                await File.WriteAllTextAsync(progressFile, summarizedContent);

                int reduction = currentProgress.Length - summarizedContent.Length;
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(
                    $"  Progress log summarized: {currentProgress.Length:N0} → {summarizedContent.Length:N0} chars (-{reduction:N0})");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("  [Summarization produced invalid output, keeping original]");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  [Summarization failed: {ex.Message}, keeping original]");
            Console.ResetColor();
        }

        return cost;
    }

    private static async Task SaveTasksAsync(string tasksFile, List<RalphTask> tasks)
    {
        var taskData = tasks.Select(t => new
        {
            t.Id,
            t.Title,
            Status = t.Status.ToString()
        });

        string json = JsonSerializer.Serialize(taskData, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(tasksFile, json);
    }

    private static void PrintFinalReport(
        List<RalphTask> tasks,
        double totalCost,
        int totalTurns,
        string outputDir)
    {
        Console.WriteLine();
        Console.WriteLine("════════════════════════════════════════════════════════");
        Console.WriteLine("              FINAL REPORT (Worker-Critic)");
        Console.WriteLine("════════════════════════════════════════════════════════");
        Console.WriteLine();

        int completed = tasks.Count(t => t.Status == TaskStatus.Completed);
        int failed = tasks.Count(t => t.Status == TaskStatus.Failed);
        int pending = tasks.Count(t => t.Status == TaskStatus.Pending);

        Console.WriteLine($"Tasks completed: {completed}/{tasks.Count}");
        Console.WriteLine($"Tasks failed: {failed}");
        Console.WriteLine($"Tasks pending: {pending}");
        Console.WriteLine($"Total worker turns: {totalTurns}");
        Console.WriteLine($"Total cost: ${totalCost:F4}");
        Console.WriteLine();

        Console.WriteLine("Task breakdown:");
        foreach (RalphTask task in tasks)
        {
            string icon = task.Status switch
            {
                TaskStatus.Completed => "[OK]",
                TaskStatus.Failed => "[FAIL]",
                TaskStatus.InProgress => "[...]",
                _ => "[--]"
            };

            Console.ForegroundColor = task.Status switch
            {
                TaskStatus.Completed => ConsoleColor.Green,
                TaskStatus.Failed => ConsoleColor.Red,
                _ => ConsoleColor.Gray
            };

            Console.WriteLine($"  {icon} {task.Title}");
            Console.ResetColor();
        }

        Console.WriteLine();

        // Show output files
        Console.WriteLine("Output files:");
        ShowFilesInDirectory(outputDir);
        Console.WriteLine();

        bool allSuccess = tasks.All(t => t.Status == TaskStatus.Completed);
        if (allSuccess)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("SUCCESS - All tasks completed!");
            Console.WriteLine($"Open {Path.Combine(outputDir, "portfolio.html")} in a browser to see the result.");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAILURE - Not all tasks completed.");
        }

        Console.ResetColor();
        Console.WriteLine();
    }

    private static int GetArgValue(string[] args, string argName, int defaultValue)
    {
        int idx = Array.IndexOf(args, argName);
        if (idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out int value))
        {
            return Math.Max(1, Math.Min(100, value));
        }

        return defaultValue;
    }

    private static int? GetArgValueOrNull(string[] args, string argName)
    {
        int idx = Array.IndexOf(args, argName);
        if (idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out int value))
        {
            return Math.Max(1, Math.Min(100, value));
        }

        return null;
    }

    /// <summary>
    ///     Formats token count as KB for display.
    ///     Approximately 4 chars per token, so tokens * 4 / 1024 = KB
    /// </summary>
    private static string FormatContextSize(int tokens)
    {
        return tokens switch
        {
            0 => "0",
            // Show as Xk tokens (like other examples)
            >= 1000 => $"{tokens / 1000.0:F1}k",
            _ => $"{tokens}"
        };
    }

    private static void ClearDirectory(string fullPath)
    {
        if (!Path.IsPathFullyQualified(fullPath))
        {
            throw new ArgumentException($"Path must be fully qualified: {fullPath}");
        }

        if (Directory.Exists(fullPath))
        {
            foreach (string file in Directory.GetFiles(fullPath))
            {
                File.Delete(file);
            }
        }

        Directory.CreateDirectory(fullPath);
    }

    private static void ShowFilesInDirectory(string fullPath)
    {
        if (!Directory.Exists(fullPath))
        {
            Console.WriteLine("  (directory not found)");
            return;
        }

        string[] files = Directory.GetFiles(fullPath);
        if (files.Length == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            foreach (string file in files)
            {
                FileInfo info = new(file);
                Console.WriteLine($"  - {info.Name} ({info.Length:N0} bytes)");
            }
        }
    }
}

/// <summary>
///     Task status in the ralph loop.
/// </summary>
public enum TaskStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

/// <summary>
///     Represents a task in the ralph loop.
/// </summary>
public sealed class RalphTask
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Instructions { get; init; }
    public required List<string> AcceptanceCriteria { get; init; }
    public TaskStatus Status { get; set; }
}

/// <summary>
///     Result of running a single iteration.
/// </summary>
public sealed class IterationResult
{
    public string Output { get; set; } = "";
    public double Cost { get; set; }
    public bool IsError { get; set; }
    public string? Error { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int ContextBefore { get; set; } // After warmup, before task
    public int ContextTokens { get; set; } // After task (the "after")
}

/// <summary>
///     Result of running the iteration loop for a task.
/// </summary>
public sealed class TaskResult
{
    public required string TaskId { get; init; }
    public bool Completed { get; set; }
    public int Iterations { get; set; }
    public double TotalCost { get; set; }
    public string? Error { get; set; }
}

/// <summary>
///     Result from the critic's validation of worker output.
/// </summary>
public sealed class CriticResult
{
    public bool Approved { get; set; }
    public string Feedback { get; set; } = "";
    public double Cost { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}
