using System;
using SmartAuditor.Editor.Settings;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Used to mark a numeric field in a class that inherits from <seealso cref="DiagnosticAnalyzer"/> as being a Diagnostic Parameter.
    /// </summary>
    /// <remarks>
    /// Diagnostic Parameters are used to define threshold values against which to compare other values when an Analyzer
    /// is deciding whether or not something constitutes a reportable issue. Whilst Analyzers are free to use hard-coded
    /// constants as threshold values, Diagnostic Parameters allow you to change values in Settings > Smart Auditor as
    /// a project's requirements evolve, or to set different values for different target platforms.
    ///
    /// Diagnostic Parameters and their default values are automatically registered in the <seealso cref="DiagnosticParams"/>
    /// object held by <seealso cref="SmartAuditorSettings"/>, where their values can be customized if required. When
    /// <seealso cref="SmartAuditor"/> initializes prior to running analysis, the values in the DiagnosticParams held by
    /// <seealso cref="AnalysisOptions"/> are automatically cached back in their corresponding fields which can be used
    /// during analysis.
    ///
    /// Both <c>int</c> and <c>float</c> fields are supported. The constructor overload matches the field's storage type;
    /// the reflection-time dispatch in <see cref="DiagnosticAnalyzer"/> picks the correct typed accessor on
    /// <see cref="DiagnosticParams"/>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field)]
    public class DiagnosticParameterAttribute : Attribute
    {
        /// <summary>
        /// The Diagnostic Parameter's name. This name should uniquely identify this parameter within a project.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// True when the parameter's default was supplied as a <c>float</c>; false for the <c>int</c> overload.
        /// </summary>
        public bool IsFloat { get; private set; }

        /// <summary>
        /// The integer default value supplied to the constructor. Only meaningful when <see cref="IsFloat"/> is false.
        /// </summary>
        public int DefaultValue { get; private set; }

        /// <summary>
        /// The float default value supplied to the constructor. Only meaningful when <see cref="IsFloat"/> is true.
        /// </summary>
        public float DefaultFloatValue { get; private set; }

        /// <summary>
        /// Constructor for an integer-valued parameter.
        /// </summary>
        /// <param name="name">The Diagnostic Parameter's name</param>
        /// <param name="defaultValue">A default value for the parameter</param>
        public DiagnosticParameterAttribute(string name, int defaultValue)
        {
            Name = name;
            DefaultValue = defaultValue;
            IsFloat = false;
        }

        /// <summary>
        /// Constructor for a float-valued parameter.
        /// </summary>
        /// <param name="name">The Diagnostic Parameter's name</param>
        /// <param name="defaultValue">A default value for the parameter</param>
        public DiagnosticParameterAttribute(string name, float defaultValue)
        {
            Name = name;
            DefaultFloatValue = defaultValue;
            IsFloat = true;
        }
    }
}
