
namespace GirlsDevGames.KatyaAi
{
	public enum Status { Running, Success, Failure }
	
	// Base interface for all tickable nodes
	public interface ITickable
	{
		Status Tick();
		void Reset();
	}
}
