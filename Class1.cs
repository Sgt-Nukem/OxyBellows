using HarmonyLib;
using KMod;
using PeterHan.PLib.Actions;
using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

namespace OxyBellows
{
	internal static class Shared
	{
		internal static Action KleiAction;
	}

	public class OxyBellowsMod : UserMod2
    {
        private static readonly string TOOL_NAME = "( °  )(  ° )";

		public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            PUtil.InitLibrary();
			Shared.KleiAction = new PActionManager().CreateAction("Air Bellows", NAME, new PKeyBinding()).GetKAction();
        }

        [HarmonyPatch(typeof(PlayerController), "OnPrefabInit")]
        public static class PlayerController_OnPrefabInit
        {
            public static void Postfix(PlayerController __instance)
            {
                List<InterfaceTool> interfaceToolList = new List<InterfaceTool>(__instance.tools);
                GameObject gameObject = new GameObject(TOOL_NAME);
                gameObject.AddComponent<OxyBellowsTool>();
                gameObject.transform.SetParent(__instance.gameObject.transform);
                interfaceToolList.Add(gameObject.GetComponent<InterfaceTool>());
                __instance.tools = interfaceToolList.ToArray();
            }
        }

        [HarmonyPatch(typeof(ToolMenu), "CreateBasicTools")]
        public static class ToolMenu_CreateBasicTools
        {
            public static void Prefix(ToolMenu __instance) => __instance.basicTools.Add(ToolMenu.CreateToolCollection(NAME, "sprinkle", Shared.KleiAction, TOOL_NAME, (LocString)string.Format("An air bellows to move minimal quantities of gas {0}", (object)"{Hotkey}"), false));
        }

		private static readonly LocString NAME = (LocString)"Bellows";
	}

	public class OxyBellowsTool : InterfaceTool
	{
		private const float Capacity = 0.1f;

		private int? rememberedCellIndex;

		public override void OnLeftClickDown(Vector3 mouseCursorPos)
		{
			int clickedCellIndex = Grid.PosToCell(mouseCursorPos);

			if (this.rememberedCellIndex == null) {
				if (Grid.IsValidCell(clickedCellIndex)) {
					this.rememberedCellIndex = clickedCellIndex;
					PopFXManager.Instance.SpawnFX(PopFXManager.Instance.sprite_Negative, "Source cell set", null, mouseCursorPos, force_spawn: true);
				}
			}
			else this.tryMoveAir((int)rememberedCellIndex, clickedCellIndex, mouseCursorPos);
		}

		private void tryMoveAir(int sourceCellIndex, int targetCellIndex, Vector3 popUpFxCoords)
		{
			if (!Grid.IsValidCell(sourceCellIndex))
			{
				PopFXManager.Instance.SpawnFX(PopFXManager.Instance.sprite_Negative, "Invalid source cell", null, popUpFxCoords, force_spawn: true);
				return;
			}

			if (!Grid.IsValidCell(targetCellIndex))
			{
				PopFXManager.Instance.SpawnFX(PopFXManager.Instance.sprite_Negative, "Invalid target cell", null, popUpFxCoords, force_spawn: true);
				return;
			}

			if(!Grid.IsGas(sourceCellIndex))
			{
				PopFXManager.Instance.SpawnFX(PopFXManager.Instance.sprite_Negative, "Source cell contains no gas", null, popUpFxCoords, force_spawn: true);
				return;
			}

			if (Grid.IsSolidCell(targetCellIndex))
			{
				PopFXManager.Instance.SpawnFX(PopFXManager.Instance.sprite_Negative, "Target cell is solid", null, popUpFxCoords, force_spawn: true);
				return;
			}

			if (!Grid.IsGas(targetCellIndex) && !Grid.IsLiquid(targetCellIndex) && !Grid.Element[targetCellIndex].IsVacuum)
			{
				PopFXManager.Instance.SpawnFX(PopFXManager.Instance.sprite_Negative, "Target cell contains neither gas nor liquid nor vacuum", null, popUpFxCoords, force_spawn: true);
				return;
			}

			var elementToMove = Grid.Element[sourceCellIndex];
			var elementTemperature = Grid.Temperature[sourceCellIndex];
			var elementContaminationType = Grid.DiseaseIdx[sourceCellIndex];
			var elementContaminationAmount = Grid.DiseaseCount[sourceCellIndex];

			var massToMove = Math.Min(Grid.Mass[sourceCellIndex], Capacity);
			if (massToMove > 0.0f)
			{
				if (Grid.IsGas(targetCellIndex) || Grid.Element[targetCellIndex].IsVacuum)
				{
					SimMessages.ConsumeMass(sourceCellIndex, elementToMove.id, massToMove, (byte)1);
					SimMessages.EmitMass(targetCellIndex, elementToMove.idx, massToMove, elementTemperature, elementContaminationType, elementContaminationAmount);

					UISounds.PlaySound(UISounds.Sound.ClickObject);
					PopFXManager.Instance.SpawnFX(PopFXManager.Instance.sprite_Negative, "Emitting gas", null, popUpFxCoords, force_spawn: true);
					return;
				}
				else if (Grid.IsLiquid(targetCellIndex))
				{
					SimMessages.ConsumeMass(sourceCellIndex, elementToMove.id, massToMove, (byte)1);

					CellElementEvent sandBoxTool = CellEventLogger.Instance.SandBoxTool;
					SimMessages.ReplaceAndDisplaceElement(targetCellIndex, elementToMove.id, sandBoxTool, massToMove, elementTemperature, elementContaminationType, elementContaminationAmount);

					UISounds.PlaySound(UISounds.Sound.ClickObject);
					PopFXManager.Instance.SpawnFX(PopFXManager.Instance.sprite_Negative, "Displacing gas", null, popUpFxCoords, force_spawn: true);
					return;
				}
			}
			else
			{
				PopFXManager.Instance.SpawnFX(PopFXManager.Instance.sprite_Negative, "No mass to move", null, popUpFxCoords, force_spawn: true);
				return;
			}
		}

		public override void OnRightClickDown(Vector3 mouseCursorPos, KButtonEvent buttonEvent)
		{
			if (this.rememberedCellIndex != null)
			{
				buttonEvent.TryConsume(Shared.KleiAction);
				buttonEvent.Consumed = true;

				this.rememberedCellIndex = null;
				PopFXManager.Instance.SpawnFX(PopFXManager.Instance.sprite_Negative, "Source cell unset.", null, mouseCursorPos, force_spawn: true);
			}
			//else
			//{
			//	PopFXManager.Instance.SpawnFX(PopFXManager.Instance.sprite_Negative, "Air bellows tool deactivated", null, mouseCursorPos, force_spawn: true);
			//}
		}
	}
}