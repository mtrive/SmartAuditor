namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Provides methods to construct a <seealso cref="ReportItem"/> object for a Report.
    /// </summary>
    public class ReportItemBuilder
    {
        ReportItem m_Item;

        /// <summary>Implicit conversion of ReportItemBuilder to ReportItem.</summary>
        /// <param name="builder">A ReportItemBuilder to convert</param>
        /// <returns>A ReportItem</returns>
        public static implicit operator ReportItem(ReportItemBuilder builder) => builder.m_Item;

        /// <summary>
        /// Constructor for an object to build ReportItems representing Issues.
        /// </summary>
        /// <param name="category">The <see cref="AnalysisCategory"/> of the reported issue</param>
        /// <param name="id">Identifies the Descriptor object containing information about the Issue</param>
        /// <param name="args">Arguments to be used in the message formatting</param>
        public ReportItemBuilder(AnalysisCategory category, string id, params object[] args)
        {
            m_Item = new ReportItem(category, id, args);
        }

        /// <summary>
        /// Set a single custom property by name. Use the property's canonical key, which
        /// matches the analyzer-side enum value name by convention.
        /// </summary>
        /// <param name="name">Canonical property name (no whitespace).</param>
        /// <param name="value">Value to store, stringified via <see cref="object.ToString"/>.</param>
        public ReportItemBuilder WithProperty(string name, object value)
        {
            m_Item.SetProperty(name, value);
            return this;
        }

        /// <summary>
        /// Adds a description string to the ReportItem being built.
        /// </summary>
        /// <param name="description">Description string to add</param>
        /// <returns>The ReportItemBuilder object with the description string added</returns>
        public ReportItemBuilder WithDescription(string description)
        {
            m_Item.Description = description;
            return this;
        }

        /// <summary>
        /// Adds a DependencyNode to the ReportItem being built.
        /// </summary>
        /// <param name="dependencies">The root DependencyNode of a dependency chain to add</param>
        /// <returns>The ReportItemBuilder object with the DependencyNode added</returns>
        public ReportItemBuilder WithDependencies(DependencyNode dependencies)
        {
            m_Item.Dependencies = dependencies;
            return this;
        }

        /// <summary>
        /// Adds a Location to the ReportItem being built.
        /// </summary>
        /// <param name="location">Location object describing where the specific item was found within the project</param>
        /// <returns>The ReportItemBuilder object with the Location added</returns>
        public ReportItemBuilder WithLocation(Location location)
        {
            m_Item.Location = location;
            return this;
        }

        /// <summary>
        /// Constructs a Location object and adds it to the ReportItem being built.
        /// </summary>
        /// <param name="path">File path within the project describing where the specific item was found</param>
        /// <param name="line">Start line number within the file</param>
        /// <param name="endLine">Optional end line number for multi-line locations</param>
        /// <param name="column">Optional start column number</param>
        /// <param name="endColumn">Optional end column number</param>
        /// <returns>The ReportItemBuilder object with the Location added</returns>
        public ReportItemBuilder WithLocation(string path, int? line = null, int? endLine = null, int? column = null, int? endColumn = null)
        {
            m_Item.Location = new Location(path, line, endLine, column, endColumn);
            return this;
        }

        /// <summary>
        /// Adds a LogLevel to the ReportItem being built.
        /// </summary>
        /// <param name="logLevel">Log Level of the item</param>
        /// <returns>The ReportItemBuilder object with the LogLevel added</returns>
        public ReportItemBuilder WithLogLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Error:
                    m_Item.Severity = Severity.Error;
                    break;
                case LogLevel.Warning:
                    m_Item.Severity = Severity.Warning;
                    break;
                case LogLevel.Info:
                    m_Item.Severity = Severity.Info;
                    break;
            }
            return this;
        }

        /// <summary>
        /// Adds a Severity to the ReportItem being built.
        /// </summary>
        /// <param name="severity">Severity of the item</param>
        /// <returns>The ReportItemBuilder object with the LogLevel added</returns>
        public ReportItemBuilder WithSeverity(Severity severity)
        {
            m_Item.Severity = severity;
            return this;
        }
    }
}
