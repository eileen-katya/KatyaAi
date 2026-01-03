using System;
using GirlsDevGames.KatyaAi;

public class SmartAgentBase<TState> where TState : Enum
{
	public SmartStateMachine<TState> smart_sm;
	
	public SmartAgentBase(TState initial_state)
	{		
        smart_sm = new(
			initial_state,
			methodChangeCallback: OnMethodChange
		);
	}

	public virtual void Define_State(
		TState state,
		Action Update = null,
		Func<bool> Enter  = null,
		Func<bool> Exit   = null) {

		smart_sm.GetSM().AddState(
			state.ToString(),
			(int)(object)state,
			Update,
			Enter,
			Exit);
	}

	public virtual void Update()
	{
		smart_sm.Update();
	}
	
	protected virtual void OnMethodChange(int state_id, int method_id)
	{
	}
}
