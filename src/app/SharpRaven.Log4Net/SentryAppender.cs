using System;
using System.Collections.Generic;
using System.Linq;
using SharpRaven.Data;
using SharpRaven.Log4Net.Extra;
using log4net.Appender;
using log4net.Core;
using log4net.Util;

namespace SharpRaven.Log4Net
{
    public class SentryAppender : AppenderSkeleton
    {
        private readonly object clientLocker = new object();
        private RavenClient ravenClient;

        public string DSN { get; set; }
        public string Logger { get; set; }
        public string Release { get; set; }
        private readonly IList<SentryTag> tagLayouts = new List<SentryTag>();

        private RavenClient GetRavenClient()
        {
            if (ravenClient == null)
            {
                lock (clientLocker)
                {
                    if (ravenClient == null)
                    {
                        ravenClient = new RavenClient(DSN)
                        {
                            Logger = this.Logger,
                            Release = this.Release,
                            ErrorOnCapture = ex => LogLog.Error(typeof(SentryAppender), "[" + Name + "] " + ex.Message, ex)
                        };
                    }
                }
            }

            return ravenClient;
        }

        public void SetLoggingErrorHandler(Action<Exception> handler)
        {
            GetRavenClient().ErrorOnCapture = handler;
        }

        public void SetBeforeSendHandler(Func<Requester, Requester> handler)
        {
            GetRavenClient().BeforeSend = handler;
        }

        public void AddTag(SentryTag tag)
        {
            tagLayouts.Add(tag);
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            var httpExtra = HttpExtra.GetHttpExtra();
            object extra;

            if (httpExtra != null)
            {
                extra = new
                {
                    Environment = new EnvironmentExtra(),
                    Http = httpExtra,
                    AppMessage = loggingEvent.RenderedMessage
                };
            }
            else
            {
                extra = new
                {
                    Environment = new EnvironmentExtra(),
                    AppMessage = loggingEvent.RenderedMessage
                };
            }

            var tags = tagLayouts.ToDictionary(t => t.Name, t => (t.Layout.Format(loggingEvent) ?? string.Empty).ToString());

            var exception = loggingEvent.ExceptionObject ?? loggingEvent.MessageObject as Exception;
            var level = Translate(loggingEvent.Level);

            SentryEvent sentryEvent;

            if (exception != null)
            {
                sentryEvent = new SentryEvent(exception);
            }
            else
            {
                var message = loggingEvent.RenderedMessage;
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = "No message";
                }
                sentryEvent = new SentryEvent(new SentryMessage(message));
            }

            sentryEvent.Extra = extra;
            sentryEvent.Tags = tags;
            sentryEvent.Level = level;

            GetRavenClient().Capture(sentryEvent);
        }

        protected override void Append(LoggingEvent[] loggingEvents)
        {
            foreach (var loggingEvent in loggingEvents)
            {
                Append(loggingEvent);
            }
        }

        internal static ErrorLevel Translate(Level level)
        {
            switch (level.DisplayName)
            {
                case "WARN":
                    return ErrorLevel.Warning;

                case "NOTICE":
                    return ErrorLevel.Info;
            }

            ErrorLevel errorLevel;

            return !Enum.TryParse(level.DisplayName, true, out errorLevel)
                ? ErrorLevel.Error
                : errorLevel;
        }
    }
}
