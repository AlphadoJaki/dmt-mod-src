using DMT;
using Harmony;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

using System;
//using HarmonyLib;
using WorldGenerationEngine;

[HarmonyPatch(typeof(GenerationManager))]
[HarmonyPatch("GenerateTowns")]
public class AlphadoJaki_HubGen_TrySmallerWithoutAbort : IHarmony
{
	public static readonly bool isDebug = false;
	public static readonly int[][] HubMaxCount = new int[][]
	{
		new int[]// 8K Map | DON'T SWAP THIS WITH 4K
		{//         Max : 256 of Rural
			 32,     //City  (512 ~ 682m) Default  2
			 48,     //Town  (682 ~ 853m) Default  5
			255      //Rural (853 ~1024m) Default 10
		},
		new int[]// 4K Map | DON'T SWAP THIS WITH 8K
		{//         Max : 32 of Rural
			 8,     //City  (512 ~ 682m) Default  1
			12,     //Town  (682 ~ 853m) Default  2
			32      //Rural (853 ~1024m) Default  5
		},
		new int[]// 16K Map
		{//         Max : 1024 of Rural
			 128,     //City  (512 ~ 682m) Default  4
			 180,     //Town  (682 ~ 853m) Default 10
			1024      //Rural (853 ~1024m) Default 15
		}
	};

	public void Start()
	{
		try
		{
			Debug.Log("  Loading Patch: " + GetType().ToString());
			var harmony = HarmonyInstance.Create(GetType().ToString());
			harmony.PatchAll(Assembly.GetExecutingAssembly());
		}
		catch (Exception ex)
		{
				Log.Error("Transpiler aborted because of Exception:");
				Log.Error(ex.Message);
				Log.Error(ex.StackTrace);
				throw;
		}
	}

	public static IEnumerable<CodeInstruction> InternalTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilgen)
	{
		var codes = new LinkedList<CodeInstruction>(instructions);

		var itr = codes.First;

		var concatMethod = AccessTools.Method(AccessTools.TypeByName("System.String"), "Concat", new Type[]{typeof(object), typeof(object)});
		var basicLogMethod = AccessTools.Method(AccessTools.TypeByName("Log"), "Out", new Type[]{typeof(string)});

		Info("Replacing Hard-Max town count 32 to size/512 : 1 of 2");
		while(itr.Value.opcode != OpCodes.Stloc_0)
			itr = itr.Next;
		codes.Remove(itr.Next);
		codes.Remove(itr.Next);
		itr = itr.Next;

		var generationRulesInstance = AccessTools.Field(typeof(WorldGenerationEngine.GenerationRules), "Instance");
		var generationSize = AccessTools.Method(typeof(WorldGenerationEngine.GenerationRules), "get_WorldSize");
		codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldsfld, generationRulesInstance));
		codes.AddBefore(itr, new CodeInstruction(OpCodes.Callvirt, generationSize));
		codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldc_I4, 512));
		codes.AddBefore(itr, new CodeInstruction(OpCodes.Div));
		codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldsfld, generationRulesInstance));
		codes.AddBefore(itr, new CodeInstruction(OpCodes.Callvirt, generationSize));
		codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldc_I4, 512));
		codes.AddBefore(itr, new CodeInstruction(OpCodes.Div));

		if(isDebug)
		{
			var itrBk = itr;
			while(itr != codes.Last &&
				!(itr.Previous.Value.opcode == OpCodes.Call &&
				itr.Previous.Previous.Value.opcode == OpCodes.Call &&
				itr.Previous.Previous.Previous.Value.opcode == OpCodes.Call &&
				itr.Previous.Previous.Previous.Previous.Value.opcode == OpCodes.Mul))
				itr = itr.Next;
			if(itr == codes.Last)
				Error("  NO endFor in socket height calc");

			var arrayGetLength = AccessTools.Method(typeof(System.Array), "GetLength");

			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldstr, "[MODS,alf,HPF]Hub height calc "));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldloc_1));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldc_I4_1));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Callvirt, arrayGetLength));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldloc_S, 9));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Mul));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldloc_S, 11));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Add));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldc_I4_1));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Add));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Box,typeof(Int32)));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Call, concatMethod));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldstr, " of "));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Call, concatMethod));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldloc_1));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldc_I4_0));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Callvirt, arrayGetLength));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldloc_1));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldc_I4_1));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Callvirt, arrayGetLength));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Mul));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Box,typeof(Int32)));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Call, concatMethod));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Call, basicLogMethod));

			itr = itrBk;
		}

		Info("Replacing Max bound for each Hub-type");
		while(itr.Value.labels.Count == 0)
			itr = itr.Next;
		var forLabel = itr.Value.labels[0];
		while(!(itr.Value.operand as Label?).Equals(forLabel) && itr != codes.Last)
			itr = itr.Next;
		if(itr == codes.Last)
			Error("  NO endFor Op");
		itr = itr.Next; // int 8K.City = 2;

		//Info(itr.Next.Value.ToString());
		var townNumCfgHook = itr.Next.Value.Clone();// Stloc_S 8K.City
		for(int i = 0; i < 3; i++)
		{
			//while(!itr.Value.Equals(townNumCfgHook) &&
			while(!(itr.Value.opcode == townNumCfgHook.opcode &&
			itr.Value.operand == townNumCfgHook.operand) &&
			itr != codes.Last)
				itr = itr.Next;
			if(itr == codes.Last)
				Error("  NO townNumberConfiguration Op");
			for(int hubRank = 0; hubRank < 3; hubRank++)
			{
				Info("  townNumCfg  Size : " + i + ", Rank : " + hubRank);
				//Info("  Replacing : " + itr.Previous.Value.ToString());
				//Info("  Comes before : " + itr.Value.ToString());
				itr.Previous.Value = new CodeInstruction(OpCodes.Ldc_I4, HubMaxCount[i][hubRank] as Int32?);
				itr = itr.Next.Next;
			}
		}// int generatedCity = 0;

		if(isDebug)
		{
			var itrBk = itr;
			itr = itr.Next;

			while(itr != codes.Last && itr.Value.labels.Count == 0)
				itr = itr.Next;
			if(itr == codes.Last)
				Error("  NO for : Pack Valid Town IDX List");
			var packValidTownIdxFor = itr.Value.labels[0];
			while(itr != codes.Last && !(itr.Value.operand as Label?).Equals(packValidTownIdxFor))
				itr = itr.Next;
			if(itr == codes.Last)
				Error("  NO end for : Pack Valid Town IDX List");
			itr = itr.Next;
			// XUtils.Shuffle<Vector2i>(GenerationRules.Instance.Seed, ref list);

			var getTownCandidate = AccessTools.Method(typeof(List<Vector2i>), "get_Count");

			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldstr, "[MODS,alf,HPF]Found "));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldloc, 8));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Callvirt, getTownCandidate));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Box,typeof(Int32)));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Call, concatMethod));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldstr, " Hubs at valid elevation"));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Call, concatMethod));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Call, basicLogMethod));

			while(itr != codes.Last && itr.Value.labels.Count == 0)
				itr = itr.Next;
			if(itr == codes.Last)
				Error("  NO for : RandomHubSize and TryGen");
			var randomHubSizeNTryGenFor = itr.Value.labels[0];

			while(itr != codes.Last && itr.Value.opcode != OpCodes.Stloc_S)
				itr = itr.Next;
			if(itr == codes.Last)
				Error("  NO GetX from Valid Town IDX List");
			itr = itr.Next;

			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldstr, "[MODS,alf,HPF]Trying Hub Gen "));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldloc, 15));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldc_I4_1));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Add));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Box,typeof(Int32)));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Call, concatMethod));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldstr, " of "));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Call, concatMethod));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldloc, 8));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Callvirt, getTownCandidate));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Box,typeof(Int32)));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Call, concatMethod));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Call, basicLogMethod));

			while(itr != codes.Last && !(itr.Value.operand as Label?).Equals(randomHubSizeNTryGenFor))
				itr = itr.Next;
			if(itr == codes.Last)
				Error("  NO end for : RandomHubSize and TryGen");

			while(itr != codes.Last && itr.Value.labels.Count == 0)
				itr = itr.Next;
			if(itr == codes.Last)
				Error("  NO for : Highways Generation");
			//var highwayGenFor = itr.Value.labels[0];

			while(itr != codes.Last && itr.Value.opcode != OpCodes.Stloc_S)
				itr = itr.Next;
			if(itr == codes.Last)
				Error("  NO Run Highway");
			itr = itr.Next;

			var townBuilder = AccessTools.Field(typeof(WorldGenerationEngine.GenerationManager), "TownBuilders");
			var getRoadCount = AccessTools.Method(typeof(List<WorldGenerationEngine.SocketTownBuilder>), "get_Count");

			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldstr, "[MODS,alf,HPF]Generating Highway "));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldloc, 31));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldc_I4_1));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Add));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Box,typeof(Int32)));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Call, concatMethod));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldstr, " of "));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Call, concatMethod));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldarg_0));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldfld, townBuilder));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Callvirt, getRoadCount));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Box,typeof(Int32)));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Call, concatMethod));
			codes.AddBefore(itr, new CodeInstruction(OpCodes.Call, basicLogMethod));

			itr = itrBk;
		}

		Info("Reviving dead rural");
		int codeChanged = 0;
		for(; codeChanged < 2 && itr != codes.Last; itr = itr.Next)
		{
			//Info("  OpCode:" + itr.Value.opcode.ToString());
			if(itr.Value.opcode == OpCodes.Ldc_R4)
			{
				if((Math.Abs((itr.Value.operand as float?).Value - 0.666f) < 0.01) ||
					(Math.Abs((itr.Value.operand as float?).Value - 0.333f) < 0.01))
				{
					int convCount = 0;
					for(var insertPos = itr; insertPos != codes.First; insertPos = insertPos.Previous)
					{
						//Info("  Going back OpCode:" + insertPos.Value.opcode.ToString());
						if(insertPos.Value.opcode == OpCodes.Conv_R4)
							convCount++;
						if(convCount == 2)
						{
							codes.AddBefore(insertPos, new CodeInstruction(OpCodes.Ldloc_S, 18 as Int32?));
							codes.AddBefore(insertPos, new CodeInstruction(OpCodes.Sub));
							Info("OP change count :" + (++codeChanged) + "");
							break;
						}
					}
				}
			}
		}
		if(itr == codes.Last)
			Error("  NO hinting Constant");

		Info("Adding : if RNG match rural, rural-flag on");
		while(itr.Value.opcode != OpCodes.Blt_Un_S &&
		itr.Value.opcode != OpCodes.Blt_Un &&
		itr != codes.Last)
			itr = itr.Next;
		if(itr == codes.Last)
			Error("  NO elseIf Op");

		var jumpOpToElse = itr;
		var endIfLabel = (itr.Value.operand as Label?).Value;

		while(!itr.Value.labels.Contains(endIfLabel) && itr != codes.Last)
			itr = itr.Next;
		if(itr == codes.Last)
			Error("  NO ENDIF LABEL");

		codes.AddBefore(itr, new CodeInstruction(OpCodes.Br, endIfLabel));
		codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldc_I4_1)); // elseLabel
		codes.AddBefore(itr, new CodeInstruction(OpCodes.Stloc_S, 24));
		Info("  else Ops injected");

		var elseLabel = ilgen.DefineLabel();
		itr.Previous.Previous.Value.labels.Add(elseLabel);
		jumpOpToElse.Value.operand = elseLabel;
		Info("  label Integrity fixed");


		itr = itr.Next.Next.Next.Next.Next;/*
		var getTownCandidate = AccessTools.Method(typeof(List<Vector2i>), "get_Count");

		codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldloc, 8));
		codes.AddBefore(itr, new CodeInstruction(OpCodes.Callvirt, getTownCandidate));
		codes.AddBefore(itr, new CodeInstruction(OpCodes.Box,typeof(Int32)));
		codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldstr, " of v2i found"));
		codes.AddBefore(itr, new CodeInstruction(OpCodes.Call, concatMethod));
		codes.AddBefore(itr, new CodeInstruction(OpCodes.Call, basicLogMethod));*/

		Info("label detection for later use");
		while(
		// !(itr.Value.opcode == OpCodes.Ldloc_S &&
		// (itr.Value.operand as uint?).Equals(24) &&
		// itr.Next.Value.opcode == OpCodes.Brfalse) &&
		itr != codes.Last)
		{
			Info(itr.Value.ToString());
			if(itr.Value.opcode == OpCodes.Ldloc_S)
				Info("  " + ((LocalBuilder)itr.Value.operand).LocalIndex.ToString());
			if(itr.Value.opcode == OpCodes.Ldloc_S && itr.Next.Value.opcode == OpCodes.Brfalse)
				if(((LocalBuilder)itr.Value.operand).LocalIndex == 24)
					break;
			itr = itr.Next;
		}
		// Info(itr.Value.ToString());
		// Info("  " + (itr.Value.opcode == OpCodes.Ldloc_S) +
		// (itr.Value.operand as uint?).Equals(24) +
		// (itr.Next.Value.opcode == OpCodes.Brfalse));

		var retryTownGenLabel = (itr.Next.Value.operand as Label?).Value;
		Info("  RetryHubGen Label Found");
		if(itr.Next.Next.Next.Next.Value.opcode == OpCodes.Bge)
		{
			Info("  SkipTownGen Label Found");
			itr = itr.Next.Next.Next.Next;
		}
		if(itr == codes.Last)
			Info("  NO DoTownGen section FOUND");
		var skipTownGenLabel = (itr.Value.operand as Label?).Value;

		Info("Replacing Hard-Max town count 32 to size/512 : 2 of 2");
		for(int i = 0; i < 2; i++)
		{
			while(itr.Value.opcode != OpCodes.Stloc_S && itr.Value.opcode != OpCodes.Bge && itr != codes.Last)
				itr = itr.Next;
			for(int j = 0; j < 2; j++)
			{
				Info(itr.Value.ToString());
				while(itr.Next.Value.opcode != OpCodes.Ldsfld && itr != codes.Last)
					itr = itr.Next;
				while(itr.Next.Value.opcode != OpCodes.Div && itr != codes.Last)
					codes.Remove(itr.Next);
				if(itr == codes.Last)
					Error("  NO 8 found");
				codes.Remove(itr.Next);
				itr = itr.Next;
				codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldc_I4, 512));
				Info(itr.Value.ToString());
			}
		}

		Info("Skiping until reaching end of town gen try");
		while(!itr.Value.labels.Contains(skipTownGenLabel) &&
		itr != codes.Last)
			itr = itr.Next;
		if(itr == codes.Last)
			Error("  NO DoTownGen End section FOUND");

		Info("  Injecting AbortRural");
		codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldloc, 24));
		codes.AddBefore(itr, new CodeInstruction(OpCodes.Brtrue, skipTownGenLabel));

		Info("  Injecting AbortCity");
		var abortTownLabel = ilgen.DefineLabel();
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Ldloc_S, 22));//AbortCity
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Brfalse, abortTownLabel));
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Ldloc_S, 5));
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Ldloc_2));
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Blt, abortTownLabel));
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Ldloc_S, 19));
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Stloc_S, 21));
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Ldc_I4_1));
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Stloc_S, 23));
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Ldc_I4_0));
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Stloc_S, 22));

		Info("  Injecting AbortTown");
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Ldloc_S, 23));
		itr.Previous.Value.labels.Add(abortTownLabel);
		var retryTownGenCheckLabel = ilgen.DefineLabel();
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Brfalse, retryTownGenCheckLabel));
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Ldloc_S, 6));
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Ldloc_3));
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Blt, retryTownGenCheckLabel));
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Ldloc_S, 18));
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Stloc_S, 21));
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Ldc_I4_0));
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Stloc_S, 23));
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Ldc_I4_1));
		codes.AddBefore(itr,new CodeInstruction(OpCodes.Stloc_S, 24));

		Info("  Injecting RetryHubGen");
		codes.AddBefore(itr, new CodeInstruction(OpCodes.Ldloc, 29));// flag4
		itr.Previous.Value.labels.Add(retryTownGenCheckLabel);
		codes.AddBefore(itr, new CodeInstruction(OpCodes.Brtrue, retryTownGenLabel));


		return codes.AsEnumerable();
	}

	public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilgen)
	{
		var l=InternalTranspiler(instructions, ilgen).ToList(); // name your actual transpiler InternalTranspiler
		string s="Code:";
		int i=0;
		foreach (var c in l) {
			if (c.opcode==OpCodes.Call ||
				c.opcode==OpCodes.Callvirt) { // you can make certain operations more visible
				//Warning(""+i+": "+c);
			} else {
				//Info(""+i+": "+c);
			}
			s+="\n"+i+": "+c;
			i++;
			yield return c;
		}
		Info(s); // or just print the entire thing out to copy to a text editor.
	}

	public static void Info(string message)
	{
		if(isDebug)
			Log.Out("[MODS,alf,HPF]" + message);
	}
	public static void Error(string message)
	{
		if(isDebug)
			Log.Error("[MODS,alf,HPF]" + message);
	}
	public static void Warning(string message)
	{
		if(isDebug)
			Log.Warning("[MODS,alf,HPF]" + message);
	}
}
