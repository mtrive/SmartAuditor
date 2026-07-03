// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Fluent builder for <see cref="Message"/>. Mutates in place so analyzers can chain calls
    /// after the builder has been handed back from <see cref="AnalysisContext.AddMessage"/>.
    /// The orchestrator finalises the builder into an immutable <see cref="Message"/> at flush time.
    /// </summary>
    public sealed class MessageBuilder
    {
        readonly AnalysisCategory m_Category;
        readonly string m_Description;
        LogLevel m_LogLevel = LogLevel.Info;
        Location m_Location;
        Dictionary<string, string> m_Properties;

        internal MessageBuilder(AnalysisCategory category, string description)
        {
            if (string.IsNullOrEmpty(description))
                throw new ArgumentException("Message description cannot be null or empty", nameof(description));

            m_Category = category;
            m_Description = description;
        }

        public MessageBuilder WithLogLevel(LogLevel logLevel)
        {
            m_LogLevel = logLevel;
            return this;
        }

        public MessageBuilder WithLocation(Location location)
        {
            m_Location = location;
            return this;
        }

        public MessageBuilder WithLocation(string path, int? line = null, int? endLine = null, int? column = null, int? endColumn = null)
        {
            m_Location = new Location(path, line, endLine, column, endColumn);
            return this;
        }

        public MessageBuilder WithProperty(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Message property key cannot be null or empty", nameof(key));

            if (m_Properties == null)
                m_Properties = new Dictionary<string, string>(StringComparer.Ordinal);
            m_Properties[key] = value ?? string.Empty;
            return this;
        }

        internal Message Build()
        {
            return new Message(m_Category, m_Description, m_LogLevel, m_Location, m_Properties);
        }
    }
}
