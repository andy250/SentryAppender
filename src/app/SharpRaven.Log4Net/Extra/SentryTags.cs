using log4net.Layout;

namespace SharpRaven.Log4Net.Extra
{
    public class SentryTag
    {
        public string Name { get; set; }
        public IRawLayout Layout { get; set; }
    }
}
