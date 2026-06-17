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

    /// <summary>Small scrolling log panel shown in the window footer.</summary>
    public class LogView : VisualElement
    {
        private const int MaxEntries = 50;

        private readonly ScrollView _scroll;

        public LogView()
        {
            AddToClassList("le-log");

            var title = new Label("Log");
            title.AddToClassList("le-log__title");
            Add(title);

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

            // Keep the newest entry visible.
            _scroll.scrollOffset = new Vector2(0, float.MaxValue);
        }
    }
}
