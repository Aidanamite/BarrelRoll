using BepInEx;
using ConfigTweaks;
using HarmonyLib;
using SWS;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BarrelRoll
{
    [BepInPlugin("com.aidanamite.BarrelRoll", "Barrel Roll", VERSION)]
    [BepInDependency("com.aidanamite.ConfigTweaks")]
    public class Main : BaseUnityPlugin
    {
        public const string VERSION = "1.0.0";
        public void Awake()
        {
            new Harmony("com.aidanamite.BarrelRoll").PatchAll();
            Logger.LogInfo("Loaded");
        }
        static SimpleWave RollAngleFlow = new SimpleWave((0,0), (0.1,0.05), (0.7,1.1), (0.9,1));
        static SimpleWave RollPoseFlow = new SimpleWave((0, 0), (0.3, 1), (0.7, 1), (1, 0));
        static float MidRoll = 0.4f;
        static float MidRollLength = 0.5f;
        public static float RollOffset = 0;
        public static float DivePoseWeight = 0;
        public static float RollProgress = 1;
        public static bool IsRolling => RollProgress < 2;
        [ConfigField]
        public static KeyCode DoRoll = KeyCode.Q;
        void Update()
        {
            var doRoll = AvAvatar.pInputEnabled && Input.GetKey(DoRoll) && AvAvatar.pObject.Safe()?.GetComponent<AvAvatarController>().Safe()?.SpeedPercent() > 0.5;
            if (doRoll && !IsRolling)
                RollProgress = 0;
            else if (IsRolling)
            {
                if (!doRoll)
                {
                    if (RollProgress < MidRoll && RollProgress + Time.deltaTime >= MidRoll)
                        RollProgress += MidRollLength;
                }
                else if (RollProgress < MidRoll + MidRollLength && RollProgress + Time.deltaTime >= MidRoll + MidRollLength)
                    RollProgress -= MidRollLength;
                RollProgress += Time.deltaTime;
            }
            if (!IsRolling)
            {
                RollOffset = 0;
                DivePoseWeight = 0;
            }
            else if (RollProgress >= MidRoll && RollProgress <= MidRoll + MidRollLength)
            {
                RollOffset = (float)RollAngleFlow.Sample(MidRoll) + (RollProgress - MidRoll) / MidRollLength;
                DivePoseWeight = (float)RollPoseFlow.Sample(MidRoll);
            }
            else
            {
                var samplePoint = RollProgress;
                if (samplePoint > MidRoll)
                    samplePoint -= MidRollLength;
                RollOffset = (float)RollAngleFlow.Sample(samplePoint);
                DivePoseWeight = (float)RollPoseFlow.Sample(samplePoint);
            }
        }
    }

    [HarmonyPatch(typeof(AvAvatarController), "UpdateFlying")]
    static class Patch_VisualRoll
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var flag = false;
            for (int i = code.Count - 1; i >= 0; i--)
            {
                var c = code[i];
                if (c.operand is MethodInfo method && method.Name == "set_localEulerAngles")
                    flag = true;
                else if (flag && c.operand is FieldInfo field && field.Name == "mRoll")
                {
                    flag = false;
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_VisualRoll), nameof(ModifyRoll))));
                }
            }
            return code;
        }
        static float ModifyRoll(float original) => original + Main.RollOffset;
    }

    [HarmonyPatch(typeof(AvAvatarController), "UpdateFlyingControl")]
    static class Patch_ForceFlap
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            code.Insert(
                code.FindIndex(code.FindIndex(x => x.operand is string s && s == "WingFlap"), x => x.operand is MethodInfo m && m.Name == "GetButton") + 1,
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_ForceFlap), nameof(ModifyFlapping)))
            );
            return code;
        }
        static bool ModifyFlapping(bool original) => original && !Main.IsRolling;
    }

    [HarmonyPatch(typeof(SanctuaryPet), "PetAnimUpdate")]
    static class Patch_VisualDive
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iL)
        {
            var code = instructions.ToList();
            code.Insert(
                code.FindIndex(x => x.operand is MethodInfo m && m.Name == "get_pFlyingPitch") + 1,
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_VisualDive), nameof(ModifyDive)))
            );
            return code;
        }
        static float ModifyDive(float original) => Mathf.Lerp( original,0.1f, Main.DivePoseWeight);
    }

    public static class ExtentionMethods
    {
        public static T Safe<T>(this T instance) where T : Object => instance ? instance : null;

        public static float SpeedPercent(this AvAvatarController controller) => controller.pFlightSpeed / controller.pMaxFlightSpeed;
    }
}