using System;
using System.Collections.Generic;
using UnityEngine;

namespace GirlsDevGames.KatyaAi {
public class SmartStateMachine<TState> where TState : Enum
{
	// Global-static
	public static readonly TState AnyState = (TState)(object)-1;
	public static readonly TState InvalidState = (TState)(object)-100;
	
	// State-Machine
	private StateMachine<SmartStateMachine<TState>> state_machine;
	
	// Goal-Evaluators
	public Dictionary<TState, System.Func<float>> goal_evaluators;

	// Transitions and sub-transitions
	private Dictionary<TState, TransitionsData> transitions_map;
	private Dictionary<TState, SmartStateMachine<TState>> subtransitions_map;
	
	// First active state as set by user.
	public TState initial_goal;
	
	// State, as a result of a trigger being fired.
	private TState primary_state;  

	// Active state
	private TState active_state;
	
	// Set this to true after finalizing, see Build method.
	private bool is_built = false;

	
	// Constructor	
	public SmartStateMachine(
		TState initial_state,
		StateMachine<SmartStateMachine<TState>> state_machine = null,
		System.Action<int, int> methodChangeCallback = null) {

		if (state_machine == null) state_machine = new();
		else state_machine = state_machine;
		
		if (methodChangeCallback != null)
			state_machine.AddOnMethodSwitchCallback( methodChangeCallback );

		initial_goal = initial_state;
		primary_state = initial_state;
		active_state = initial_state;
		
		transitions_map = new();
		subtransitions_map = new();
		goal_evaluators = new();
	}

	public void AddTransition(Transition transition)
	{
		if (!HasTransitions(transition.FromState))
			transitions_map[transition.FromState] = new(new());

		// Prevent duplicates
		var transitionsData = transitions_map[transition.FromState];
		foreach (var existing in transitionsData.Transitions)
		{
			if (EqualityComparer<TState>.Default.Equals(existing.ToState, transition.ToState))
			{
				Debug.LogError($"Transition from '{transition.FromState}' to '{transition.ToState}' already exists!");
				return;
			}
		}

		transitionsData.Add(transition);

		Debug.LogFormat("Added Transition To: {0} FromState: {1}", 
			transition.ToState, transition.FromState);
	}
	
	public void AddSubTransitionsBlock(TState parent,
		SmartStateMachine<TState> subBlock) { 
		if (subtransitions_map.ContainsKey(parent))
		{
			Debug.LogError($"A sub transitions map from '{parent}' already exists!");
			return;
		}
	
		subtransitions_map[parent] = subBlock;
	}
	
	public TransitionBuilder Begin_Goal_Defs()
	{
		var context = new TransitionContext
		{ SmartStateMachine = this };
		
		return new TransitionBuilder(context);
	}
	
	public void Build()
	{
		if (transitions_map == null || transitions_map.Count == 0) {
			Debug.LogError("No transitions defined!");
			return;
		}

		// -------------------------
		// Sort all transitions according to priority
		foreach(var transitionData in transitions_map.Values)
		{
			transitionData.Transitions.Sort(
				(t1, t2) => t1.Priority.CompareTo(t2.Priority)
			);
		}

		// -------------------------
		state_machine.SetOwner(this);
		state_machine.SwitchState((int)(object)active_state);
		is_built = true; 
	}

	public TState Evaluate(TState from_state, List<TState> states = null)
	{		
		if (transitions_map == null || transitions_map.Count == 0) {
			Debug.LogError("TransitionsMap is null or no transitions defined!");
			return InvalidState;
		}

		float bestScore;
		TransitionsData transitionsData = null;
		Transition best_transition = null;

		// ------------------------------------------------------------ //
		// Recursive Evaluation in Sub-States
		if (subtransitions_map != null && subtransitions_map.TryGetValue(from_state, out var subBlock))
		{
			var evaluated_state = subBlock.Evaluate(from_state, states);

			// Keep evaluating as long as there is a deeper sub-block
			while (subBlock.subtransitions_map != null &&
				   subBlock.subtransitions_map.TryGetValue(evaluated_state, out var deeperSubBlock))
			{
				subBlock = deeperSubBlock;
				evaluated_state = subBlock.Evaluate(evaluated_state, states);
			}

			return evaluated_state;
		}

		// ------------------------------------------------------------ //
		// Local transitions
		if (GetTransitionsData(from_state, out transitionsData))
		{
			best_transition = GetBestTransition(transitionsData.Transitions, out bestScore);
			if (best_transition != null)
			{
				// Debug.Log($"Transitioning from {from_state} to {best_transition.ToState} with score {bestScore}");
				if (!EqualityComparer<TState>.Default.Equals(best_transition.ToState, active_state))
					states.Add(best_transition.ToState);

				return best_transition.ToState;
			}
		}
		
		// Uncomment to see if there was no transition for this update step.
		return from_state;
	}

	public void FireState(TState state)
	{
		// No action if primary or active state same as requeted state.
		if (primary_state.Equals(state) || active_state.Equals(state))
			return;

		// Clear previously evaluated states
		_evaluated_states.Clear();
		
		// Update data
		active_state = state;
		primary_state = state;
		
		// Log
		Debug.Log($"State '{state.ToString()}' fired. Switching to State: {state}");
	}

	public StateMachine<SmartStateMachine<TState>> GetSM()
	{ return state_machine; }
	
	public TState GetActiveState() { return active_state; }
		
	public TransitionsData GetTransitions(TState fromState) 
	{ return transitions_map[fromState]; }
		
	public bool GetTransitionsData(TState key, out TransitionsData data)
	{ return transitions_map.TryGetValue(key, out data); }

	private Transition GetBestTransition(
		List<Transition> transitions,
		out float max_score) {

		max_score = 0f;
		
		Transition best_transition = null;
		float bestScore = float.MinValue;

		foreach(var transition in transitions)
		{						
			float score = transition.Evaluator();
			
			if (score > bestScore)
			{
				bestScore = score;
				best_transition = transition;
			}
		}

		return best_transition;
	}

	public void RegisterGoalEval(TState state, System.Func<float> eval)
	{		
		goal_evaluators[state] = eval;
	}

	public void SetInitialState(TState initial)
	{
		initial_goal = initial;
	}
	
	private List<TState> _evaluated_states = new();
	public void Update()
	{
		if (!is_built) { Build(); }

		// -----------------
		// Evaluate triggers
		TState goal_state = SmartStateMachine<TState>.InvalidState;
		float bestScore = float.MinValue;

		foreach(var (key, val) in goal_evaluators)
		{						
			float score = val();
			
			if (score > bestScore)
			{
				bestScore = score;
				goal_state = key;
			}
		}
		
		if (!EqualityComparer<TState>.Default.Equals(goal_state, InvalidState))
		{
			if (!state_machine.IsInTransition())
				FireState(goal_state);
		}
		
		// -----------------
		if (_evaluated_states.Count > 0)
		{
			if (!state_machine.IsInTransition())
			{
				TState state = _evaluated_states[0];
				_evaluated_states.RemoveAt(0);
				active_state = state;
				
				// If state does not exists.
				if (!state_machine.HasState((int)(object)state)) {
					Debug.LogError($"State at index {(int)(object)state} does not exists!");
					return;
				}
				Debug.LogFormat("State switched to {0}", state.ToString());
				state_machine.SwitchState((int)(object)state);
			}
		} 

		// -----------------
		// Update StateMachine, current state
		state_machine.Update();

		// -----------------
		// Evaluate transitions from primary_state after updating
		// current state
		TState next_state = primary_state;
		TState prev_state;
		
		_evaluated_states.Clear();

		do
		{
			prev_state = next_state;
			next_state = Evaluate(prev_state, _evaluated_states);
		} while (!EqualityComparer<TState>.Default.Equals(prev_state, next_state));
	}
	
	public bool HasTransitions(TState key)
	{ return transitions_map.ContainsKey(key); }
	
	
	// ------------------------------------------------------------------------------------------- //
	// ---------------------------- Transitions Data --------------------------------------------- //
	// ------------------------------------------------------------------------------------------- //
	
	public class TransitionsData
	{
		public List<Transition> Transitions = null;

		public TransitionsData(List<Transition> transitions)
		{ Transitions = transitions; }

		public void Add(Transition transition)
		{ Transitions.Add(transition); }
	}
	
	// ------------------------------------------------------------------------------------------- //
	// ---------------------------- Transition Definitions Builder ------------------------------- //
	// ------------------------------------------------------------------------------------------- //        
	
	public class TransitionContext
	{
		// Fields
		private TState _from_state = SmartStateMachine<TState>.AnyState;
		private TState _to_state = SmartStateMachine<TState>.AnyState;
		private Func<float> _evaluator = null;
		private int _priority = -1;

		// Getters
		public TState GetFromState() { return _from_state; }
		public TState GetToState() { return _to_state; }
		
		// Setters
		public void SetFromState(TState from_state, Func<float> eval = null)
		{
			_from_state = from_state;
			if (eval != null)
				SmartStateMachine.RegisterGoalEval(from_state, eval);
		}

		public void SetToState(TState to_state, int priority = -1,
			Func<float> evaluator = null) {

			if (!EqualityComparer<TState>.Default.Equals(_to_state, to_state))
			{
				if (!EqualityComparer<TState>.Default.Equals(_to_state, SmartStateMachine<TState>.AnyState))
				{
					// Create and add transition
					var transition = new Transition(
						_from_state,
						_to_state,
						_priority,
						_evaluator
					);

					SmartStateMachine.AddTransition(transition);

					// Reset
					_to_state = SmartStateMachine<TState>.AnyState;
					_evaluator = null;
				}
			}
			
			if (!EqualityComparer<TState>.Default.Equals(to_state, SmartStateMachine<TState>.AnyState))
			{ 
				_to_state = to_state;
				_priority = priority;
				_evaluator = evaluator;
			}
		}

		// Properties
		public SmartStateMachine<TState> SmartStateMachine { get; set; } = null;
		public TransitionContext ParentTransitionContext { get; set; } = null;
		public TransitionBuilder TransitionBuilder { get; set; } = null;
		public ToBuilder ToBuilder { get; set; } = null;
	}
	
	public class TransitionBuilderBase
	{
		protected readonly TransitionContext _context;
		public TransitionBuilderBase(TransitionContext context) { _context = context; }
		public TransitionContext GetContext() => _context;
	}
	
	public class TransitionBuilder : TransitionBuilderBase
	{
		public TransitionBuilder(TransitionContext context) : base(context)
		{ _context.TransitionBuilder = this; }
		
		public ToBuilderBase From(TState from_state, Func<float> eval)
		{ 
			_context.SetFromState(from_state, eval);
			return new ToBuilderBase(_context);
		}

		public SmartStateMachine<TState> x(TransitionContext context = null)
		{
			if (context == null) context = _context;
			
			// Traverse to root
			while (context.ParentTransitionContext != null)
			{ context = context.ParentTransitionContext; }

			return context.SmartStateMachine;
		}
	}

	public class ToBuilderBase : TransitionBuilderBase
	{		
		public ToBuilderBase(TransitionContext context) 
		: base(context) {}

		public virtual TransitionRouter To(TState state,
			int priority = 0,
			Func<float> evaluator = null) {

			_context.SetToState(state, priority, evaluator);
			return new TransitionRouter(_context);
		}
	}

	public class ToBuilder : ToBuilderBase
	{		
		public ToBuilder(TransitionContext context) 
		: base(context) { _context.ToBuilder = this; }
		
		public virtual TransitionBuilder end_goal() 
		{ 
			return _context.TransitionBuilder;
		}
	}
	
	public class TransitionRouter : ToBuilder
	{
		public TransitionRouter(TransitionContext context) 
		: base(context) {}
		
		public ToBuilderBase sub_goals()
		{
			TState active_to_state = _context.GetToState();
			_context.SetToState(SmartStateMachine<TState>.AnyState);

			// Start new transition block for sub-transitions
			var sub_context = new TransitionContext();
			sub_context.SetFromState(active_to_state);
			sub_context.SmartStateMachine = new SmartStateMachine<TState>(
				active_to_state,
				state_machine: _context.SmartStateMachine.GetSM()
			);
			sub_context.ParentTransitionContext = _context;
			sub_context.TransitionBuilder = _context.TransitionBuilder;
			sub_context.ToBuilder = new ToBuilder(sub_context);

			// Register the sub-transition block
			_context.SmartStateMachine.AddSubTransitionsBlock(
				active_to_state,
				sub_context.SmartStateMachine);

			// Restrict chaining again
			return new ToBuilderBase(sub_context);
		}

		public ToBuilder x()
		{
			_context.SetToState(SmartStateMachine<TState>.AnyState);
			
			// Return parent
			if (_context.ParentTransitionContext != null)
			{ return _context.ParentTransitionContext.ToBuilder; }

			return _context.ToBuilder;
		}
		
		public override TransitionBuilder end_goal()
		{
			_context.SetToState(SmartStateMachine<TState>.AnyState);
			return base.end_goal();
		}
	}
	
	public class Transition
	{
		public TState      FromState { get; }
		public TState      ToState   { get; }
		public int         Priority  { get; }
		public Func<float> Evaluator { get; }

		public Transition(
			TState fromState,
			TState toState,
			int priority,
			Func<float> evaluator) {

			FromState = fromState;
			ToState = toState;
			Priority = priority;
			Evaluator = evaluator;
		}
	}
}}
