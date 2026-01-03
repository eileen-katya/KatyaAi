using System;
using System.Collections.Generic;

namespace GirlsDevGames.KatyaAi {
public class StateMachine<T>
{
	public static readonly int   INVALID_STATE_ID = -1;
	public static readonly int   GLOBAL_STATE_ID  = -1010;
	
	public static readonly short ENTER_FUNC       = 0;
	public static readonly short UPDATE_FUNC      = 1;
	public static readonly short EXIT_FUNC        = 2;

	private Dictionary<int, StateBase<T>> statesmap = new();

	private StateBase<T> current_state;
	private StateBase<T> global_state;
	private Func<bool> global_state_trigger;

	private System.Action<int, int> method_switch_callback;

	private int current_state_idx = INVALID_STATE_ID;
	private int next_state_idx    = INVALID_STATE_ID;
	private int last_state_idx    = INVALID_STATE_ID;

	private bool has_global_state = false;
	private bool is_in_global_state = false;
	private bool has_transition = false;

	public T Owner { get; set; }
	public StateBase<T> CurrentState => current_state;
	public Dictionary<int, StateBase<T>> StatesMap => statesmap;
	
	// Debug
	// Executing method (enter, update OR exit) of the current active state. 
	private short current_func_id = -1;

	// Constructors
	public StateMachine() { 
		ResetTransitionFlags();
	}
	
	public StateMachine(T owner) {
		Owner = owner;
		ResetTransitionFlags();
	}

	void ResetTransitionFlags()
	{ 
		has_transition = 
		exit_phase_executed = false;
		current_func_id = -1;
	}
	
	private void AddState(StateBase<T> newState, int idx)
	{
		if (!CanAddState(newState, idx)) return;
		StatesMap[idx] = newState;
		UnityEngine.Debug.LogFormat("Added state at index {0}", idx);
	}

	public bool AddState(
		string stateName,
		int idx,
		Action updateCallback,
		Func<bool> enterCallback = null,
		Func<bool> exitCallback = null) {

		var state = new StateBase<T>(Owner, stateName, updateCallback, enterCallback, exitCallback);
		if (!CanAddState(state, idx)) return false;
		AddState(state, idx);
		return true;
	}
	
	public StateBase<T> AddGlobalState(
		Func<bool> triggerCallback,
		Action updateCallback,
		Func<bool> enterCallback = null,
		Func<bool> exitCallback = null) {

		if (triggerCallback == null || updateCallback == null)
		{
			UnityEngine.Debug.LogWarning("Failed to add global state: update_callback / trigger is null or state already exists!");
			return null;
		}
		
		global_state = new StateBase<T>(
			Owner,
			"Global",
			updateCallback,
			enterCallback,
			exitCallback);
			
		global_state_trigger = triggerCallback;
		StatesMap[GLOBAL_STATE_ID] = global_state;
		has_global_state = true;
		return global_state;
	}

	public void AddOnMethodSwitchCallback(System.Action<int, int> callback)
	{
		method_switch_callback = callback;
	}
	
	public void SwitchState(int idx)
	{
		if (idx == current_state_idx || 
			idx == next_state_idx    ||
			idx == GLOBAL_STATE_ID   || 
			idx == INVALID_STATE_ID)
			return;

		if (!StatesMap.ContainsKey(idx))
		{
			UnityEngine.Debug.LogWarning($"State index {idx} does not exist.");
			return;
		}

		next_state_idx = idx;
		has_transition = true;
	}
	
	public void SwitchToLastState()
	{ 
		SwitchState(last_state_idx);
	}
	
	
	private bool exit_phase_executed = false;

	private void Transition()
	{
		if (!StatesMap.ContainsKey(next_state_idx)) {
			UnityEngine.Debug.LogWarning($"Attempted to transition to invalid state: {next_state_idx}");
			ResetTransitionFlags();
			current_func_id = -1;
			return;
		}

		// EXIT phase       
		if (current_state != null && 
			next_state_idx != current_state_idx && 
			!exit_phase_executed) {

			if (current_func_id != EXIT_FUNC &&
				current_state.HasExitCallback())
				OnSwitchMethod(EXIT_FUNC);

			if (!current_state.Exit())
				return;
		}
		
		if (!exit_phase_executed)
			exit_phase_executed = true;
		
		// Change state
		if (next_state_idx != current_state_idx) {

			last_state_idx = current_state_idx;
			current_state_idx = next_state_idx;
			current_state = StatesMap[current_state_idx];
		}

		// ENTER phase	
		if (current_func_id != ENTER_FUNC &&
			current_state.HasEnterCallback())
			OnSwitchMethod(ENTER_FUNC);

		if (!current_state.Enter())
			return;
		
		// Finalize
		ResetTransitionFlags();
	}

	public void Update()
	{
		if (has_global_state && global_state_trigger != null)
		{
			bool shouldEnterGlobal = global_state_trigger();

			if (shouldEnterGlobal && !is_in_global_state)
			{
				is_in_global_state = true;
				next_state_idx = GLOBAL_STATE_ID;
				has_transition = true;
			}
			else if (!shouldEnterGlobal && is_in_global_state)
			{
				is_in_global_state = false;

				if (last_state_idx != INVALID_STATE_ID)
				{
					next_state_idx = last_state_idx;
					has_transition = true;
				}
			}
		}

		if (has_transition)
			Transition();
		else { 
			if (current_func_id != UPDATE_FUNC)
				OnSwitchMethod(UPDATE_FUNC);
			
			current_state.Update();
		}
	}

	public void OnSwitchMethod(short method_id)
	{
		current_func_id = method_id;

		if (method_switch_callback != null)
			method_switch_callback(current_state_idx, method_id);
	}
	
	private bool CanAddState(StateBase<T> state, int idx)
	{
		if (state == null)
		{
			UnityEngine.Debug.LogWarning("State is null.");
			return false;
		}

		if (idx == INVALID_STATE_ID || idx == GLOBAL_STATE_ID)
		{
			UnityEngine.Debug.LogWarning("Invalid state index.");
			return false;
		}

		if (StatesMap.ContainsKey(idx))
		{
			UnityEngine.Debug.LogWarning($"State index {idx} already exists.");
			return false;
		}

		return true;
	}
	
	public int CurrentStateIDx()
	{
		return current_state_idx;
	}
	
	public int NextStateIDx()
	{
		return next_state_idx;
	}
	
	public string ExecInfo()
	{
		if (current_func_id == ENTER_FUNC) return $"CurrentState: {current_state} Function: Enter";
		else if (current_func_id == EXIT_FUNC) return $"CurrentState: {current_state} Function: Exit";
		else if (current_func_id == UPDATE_FUNC) return $"CurrentState: {current_state} Function: Update";
		else return "Error";
	}

	public bool HasState(int idx) 
	{ 
		return StatesMap.ContainsKey(idx);
	}
	
	public bool IsInTransition() => has_transition;

	public void SetOwner(T owner)
	{
		this.Owner = owner;

		foreach(StateBase<T> state in statesmap.Values)
		{ state.SetOwner(owner); }
	}
}}
