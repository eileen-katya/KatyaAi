using System.Runtime.InteropServices;

namespace GirlsDevGames.KatyaAi {
public static class UtilityCalculator {
	[DllImport("UtilityCalculator.dll", CallingConvention = CallingConvention.Cdecl)]
	public static extern float CalculateUtility(float[] factors, float[] weights, int length);
}}
