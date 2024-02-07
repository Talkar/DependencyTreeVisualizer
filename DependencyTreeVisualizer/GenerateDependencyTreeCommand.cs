using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using System.IO;
using Task = System.Threading.Tasks.Task;

namespace DependencyTreeVisualizer
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class GenerateDependencyTreeCommand
    {
        private readonly IDependencyGraph _dependencyGraph;
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandBaseId = 0x0100;
        public const int CommandWithCoreId = 0x0200;
        public const int CommandWithCoreAndServiceId = 0x0300;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("54bc77ba-9b89-480f-b6df-4f14bbcd3b06");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateDependencyTreeCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private GenerateDependencyTreeCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            _dependencyGraph = new DependencyGraph();
            var menuBaseCommandID = new CommandID(CommandSet, CommandBaseId);
            var menuItem = new MenuCommand(this.GenerateGraph, menuBaseCommandID);
            commandService.AddCommand(menuItem);

            var menuWithCoreCommandID = new CommandID(CommandSet, CommandWithCoreId);
            var menuItemWithCore = new MenuCommand(this.GenerateGraphWithCore, menuWithCoreCommandID);
            commandService.AddCommand(menuItemWithCore);

            var menuWithCoreAndServiceCommandID = new CommandID(CommandSet, CommandWithCoreAndServiceId);
            var menuItemWithCoreAndService = new MenuCommand(this.GenerateGraphWithCoreAndService, menuWithCoreAndServiceCommandID);
            commandService.AddCommand(menuItemWithCoreAndService);

        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static GenerateDependencyTreeCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in GenerateDependencyTreeCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new GenerateDependencyTreeCommand(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void GenerateGraph(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ExecuteGraphGeneration(false, false);
        }

        private void GenerateGraphWithCore(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ExecuteGraphGeneration(true, false);
        }
        private void GenerateGraphWithCoreAndService(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ExecuteGraphGeneration(true, true);
        }


        private void ExecuteGraphGeneration(bool includeCoreComponents, bool includeServiceComponents)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            // Get an instance of the currently running Visual Studio IDE
            DTE dte = (DTE)Package.GetGlobalService(typeof(DTE));
            if (dte == null) return;

            // Get the selected items
            var selectedItems = dte.SelectedItems;
            if (selectedItems == null || selectedItems.Count == 0) return;
            var selectedItem = selectedItems.Item(1);
            if (selectedItem?.ProjectItem == null) return;

            var projectItem = selectedItem.ProjectItem;
            // Loop through all project files
            for (short i = 1; i <= projectItem.FileCount; i++)
            {
                string filePath = projectItem.FileNames[i];
                // Check if the file extension is .tsx
                if (Path.GetExtension(filePath).Equals(".tsx", StringComparison.OrdinalIgnoreCase))
                {
                    // This is a .tsx file, proceed with generating the dependency tree
                    GenerateDependencyTree(filePath, includeCoreComponents, includeServiceComponents);
                }
            }
        }

        private void GenerateDependencyTree(string filePath, bool includeCoreComponents, bool includeServiceComponents)
        {
            _dependencyGraph.GenerateGraph(filePath, 3, includeCoreComponents, includeServiceComponents);
            // Implementation of the dependency tree generation
        }
    }
}
