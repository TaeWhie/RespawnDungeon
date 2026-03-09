using UnityEngine;

namespace Ilumisoft.GameActionSystem
{
    [AddComponentMenu("Game Action System/Tasks/Miscellaneous/Switch Materials (Task)")]
    public class SwitchMaterialsTask : GameActionTask
    {
        public Renderer target = null;

        public Material[] materials = new Material[0];

        protected override StatusCode OnExecute()
        {
            if (target != null)
            {
                target.materials = materials;
            }

            return StatusCode.Completed;
        }

        public override string GetLabel() => $"Switch Material";

        public override string GetDescription()
        {
            return "Switches out the target renderers materials";
        }
    }
}