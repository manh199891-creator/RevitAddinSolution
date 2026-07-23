using System;

namespace Antigravity.SharedParamMapper.Models
{
    public enum MappingSource
    {
        Manual,           // User điền giá trị cố định
        BuiltInParam,     // Map từ built-in parameter (Length, Area, Volume...)
        OtherSharedParam, // Copy từ shared param khác
        Formula           // Expression: "{Length} * 2"
    }

    public enum ParamScope
    {
        Instance,
        Type
    }

    public class MappingRule
    {
        public string SharedParamName { get; set; }
        public Guid SharedParamGuid { get; set; }
        public ParamScope Scope { get; set; }
        public MappingSource Source { get; set; }
        public string FixedValue { get; set; }
        public string SourceParameterName { get; set; }
        public string FormulaExpression { get; set; }
    }
}
