using System;
using System.ComponentModel.Composition;
using System.Windows.Input;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

namespace LMLocal.Infrastructure.Autocompletions.InlineCompletion
{
    /// <summary>
    /// Ctrl+Right / Ctrl+Alt+Right: accepts the next word of the visible ghost-text suggestion.
    /// </summary>
    [Export(typeof(ICommandHandler))]
    [Name(nameof(AcceptWordCommandHandler))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class AcceptWordCommandHandler : IChainedCommandHandler<RightKeyCommandArgs>
    {
        public string DisplayName => nameof(AcceptWordCommandHandler);

        public CommandState GetCommandState(RightKeyCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            return nextCommandHandler();
        }

        public void ExecuteCommand(RightKeyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {
            var mods = Keyboard.Modifiers;
            bool acceptModifier = mods == ModifierKeys.Control || mods == (ModifierKeys.Control | ModifierKeys.Alt);

            if (acceptModifier &&
                SuggestionTagger.TryGet(args.TextView, out var tagger) &&
                tagger.HasSuggestion)
            {
                tagger.AcceptNextWord();
                return;
            }

            nextCommandHandler();
        }
    }

    /// <summary>
    /// Ctrl+Down / Ctrl+Alt+Down: accepts the next line of the visible ghost-text suggestion.
    /// </summary>
    [Export(typeof(ICommandHandler))]
    [Name(nameof(AcceptLineCommandHandler))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class AcceptLineCommandHandler : IChainedCommandHandler<DownKeyCommandArgs>
    {
        public string DisplayName => nameof(AcceptLineCommandHandler);

        public CommandState GetCommandState(DownKeyCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            return nextCommandHandler();
        }

        public void ExecuteCommand(DownKeyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {
            var mods = Keyboard.Modifiers;
            bool acceptModifier = mods == ModifierKeys.Control || mods == (ModifierKeys.Control | ModifierKeys.Alt);

            if (acceptModifier &&
                SuggestionTagger.TryGet(args.TextView, out var tagger) &&
                tagger.HasSuggestion)
            {
                tagger.AcceptNextLine();
                return;
            }

            nextCommandHandler();
        }
    }
}
