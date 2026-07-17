using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antigravity.HoanThien.Models;
// In a real project, we'd use Newtonsoft.Json. We use a stub here to illustrate.

namespace Antigravity.HoanThien.Services.Analysis
{
    public interface IFinishRuleEngine
    {
        void LoadRules(string jsonPath);
        List<FinishLayerRule> GetRulesForSubstrate(SubstrateClass substrate);
    }

    public class FinishRuleEngine : IFinishRuleEngine
    {
        private List<FinishLayerRule> _rules = new List<FinishLayerRule>();

        public void LoadRules(string jsonPath)
        {
            // Placeholder: In a real implementation this parses JSON using Newtonsoft.Json
            // For now, we seed default rules based on Phase 2 spec
            _rules = new List<FinishLayerRule>
            {
                new FinishLayerRule
                {
                    TargetSubstrate = SubstrateClass.Masonry,
                    LayerKind = FinishLayerKind.Plaster,
                    ElementTypeName = "AG_Trát tường",
                    Thickness = 15,
                    TopConstraint = TopConstraintMode.CeilingPlusOffset,
                    TopOffset = 150
                },
                new FinishLayerRule
                {
                    TargetSubstrate = SubstrateClass.Concrete,
                    LayerKind = FinishLayerKind.Putty,
                    ElementTypeName = "AG_Bả tường",
                    Thickness = 5,
                    TopConstraint = TopConstraintMode.CeilingPlusOffset,
                    TopOffset = 150
                }
            };
        }

        public List<FinishLayerRule> GetRulesForSubstrate(SubstrateClass substrate)
        {
            return _rules.Where(r => r.TargetSubstrate == substrate).ToList();
        }
    }
}
