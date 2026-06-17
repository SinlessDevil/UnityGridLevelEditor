using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    public enum LogLevel
    {
        Info,
        Success,
        Error
    }

    /// <summary>Collapsible, clearable scrolling log panel shown in the window footer.</summary>
    public class LogView : VisualElement
    {
        private const int MaxEntries = 50;

        private readonly ScrollView _scroll;
        private readonly Button _collapseButton;
        private bool _collapsed;

        public LogView()
        {
            AddToClassList("le-log");

            var header = new VisualElement();
            header.AddToClassList("le-log__header");

            var title = new Label("Log");
            title.AddToClassList("le-log__title");
            header.Add(title);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            header.Add(spacer);

            var clear = new Button(Clear) { text = "Clear" };
            clear.AddToClassList("le-log__btn");
            header.Add(clear);

            _collapseButton = new Button(ToggleCollapsed) { text = "▼" };
            _collapseButton.AddToClassList("le-log__btn");
            header.Add(_collapseButton);

            Add(header);

            _scroll = new ScrollView();
            _scroll.AddToClassList("le-log__scroll");
            Add(_scroll);
        }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            var entry = new Label("• " + message);
            entry.AddToClassList("le-log__entry");
            entry.AddToClassList(level switch
            {
                LogLevel.Success => "le-log__entry--success",
                LogLevel.Error => "le-log__entry--error",
                _ => "le-log__entry--info"
            });

            _scroll.Add(entry);

            while (_scroll.contentContainer.childCount > MaxEntries)
                _scroll.contentContainer.RemoveAt(0);

            _scroll.scrollOffset = new Vector2(0, float.MaxValue);
        }

        private void Clear() => _scroll.contentContainer.Clear();

        private void ToggleCollapsed()
        {
            _collapsed = !_collapsed;
            EnableInClassList("le-log--collapsed", _collapsed);
            _scroll.style.display = _collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            _collapseButton.text = _collapsed ? "▲" : "▼";
        }
    }
}
