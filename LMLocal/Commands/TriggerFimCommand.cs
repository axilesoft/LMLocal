using System;
using System.ComponentModel.Design;
using LMLocal.Infrastructure.Autocompletions.InlineCompletion;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace LMLocal
{
    /// <summary>
    /// Alt+' - requests an inline FIM suggestion immediately for the active document.
    /// </summary>
    internal sealed class TriggerFimCommand
    {
        public const int CommandId = 0x0210;

        public static readonly Guid CommandSet = new Guid("c29700c4-7786-468f-bf99-0ecb9d69343f");

        private TriggerFimCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            if (commandService == null)
                throw new ArgumentNullException(nameof(commandService));

            commandService.AddCommand(new MenuCommand(Execute, new CommandID(CommandSet, CommandId)));
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            new TriggerFimCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            SuggestionTagger.Focused?.TriggerSuggestion();
        }
    }
}
