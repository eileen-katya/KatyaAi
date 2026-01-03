using System;
using System.Collections.Generic;
using UnityEngine;

namespace GirlsDevGames.KatyaAi {
public class BehaviorTree
{
	// --------------------
	// Composites
	// --------------------
	// Sequence: runs children in order. If a child returns Running, Sequence returns Running.
	// If any child returns Failure -> Sequence resets and returns Failure.
	// If all children return Success -> Sequence resets and returns Success.
	public class Sequence : ITickable
	{
		private readonly ITickable[] children;
		private int currentIndex = 0;

		public Sequence(params ITickable[] children) => this.children = children;

		public Status Tick()
		{
			while (currentIndex < children.Length)
			{
				var s = children[currentIndex].Tick();

				if (s == Status.Running) return Status.Running;
				if (s == Status.Failure)
				{
					Reset();
					return Status.Failure;
				}

				// Success - advance
				children[currentIndex].Reset();
				currentIndex++;
			}

			Reset();
			return Status.Success;
		}

		public void Reset() => currentIndex = 0;
	}

	// Selector: try children until one succeeds.
	// If a child returns Running, Selector returns Running.
	// If a child returns Success, Selector resets and returns Success.
	// If all children fail -> reset and return Failure.
	public class Selector : ITickable
	{
		private readonly ITickable[] children;
		private int currentIndex = 0;

		public Selector(params ITickable[] children) => this.children = children;

		public Status Tick()
		{
			while (currentIndex < children.Length)
			{
				var s = children[currentIndex].Tick();

				if (s == Status.Running) return Status.Running;
				if (s == Status.Success)
				{
					Reset();
					return Status.Success;
				}

				// Failure -> try next
				children[currentIndex].Reset();
				currentIndex++;
			}

			Reset();
			return Status.Failure;
		}

		public void Reset() => currentIndex = 0;
	}

	// PrioritySelector: always evaluate children from highest to lowest priority.
	// - Runs children in order every tick, starting at index 0.
	// - If a child returns Running, PrioritySelector returns Running
	//   and remembers which child is running. If another child later
	//   takes priority, the previously running child is Reset.
	// - If a child returns Success, PrioritySelector resets all children
	//   and returns Success immediately.
	// - If a child returns Failure, it is Reset and evaluation continues
	//   with the next child.
	// - If all children fail, PrioritySelector resets and returns Failure.
	//
	// This differs from Selector in that it does not "stick" to a child
	// index across ticks — higher-priority children are always re-checked
	// first, so urgent behaviors (e.g. "enemy spotted") can preempt
	// background ones (e.g. "patrolling").
	public class PrioritySelector : ITickable
	{
		private readonly ITickable[] children;
		private int runningIndex = -1;

		public PrioritySelector(params ITickable[] children) => this.children = children;

		public Status Tick()
		{
			for (int i = 0; i < children.Length; i++)
			{
				var s = children[i].Tick();

				if (s == Status.Running)
				{
					// if a different child was running, reset it
					if (runningIndex != -1 && runningIndex != i) children[runningIndex].Reset();
					runningIndex = i;
					return Status.Running;
				}

				if (s == Status.Success)
				{
					if (runningIndex != -1 && runningIndex != i) children[runningIndex].Reset();
					runningIndex = -1;
					Reset();
					return Status.Success;
				}

				// Failure -> reset child and try next
				children[i].Reset();
			}

			// all failed
			if (runningIndex != -1) { children[runningIndex].Reset(); runningIndex = -1; }
			return Status.Failure;
		}

		public void Reset()
		{
			foreach (var c in children) c.Reset();
			runningIndex = -1;
		}
	}

	// Parallel: options:
	// - If succeedOnFirst == true -> returns Success when ANY child returns Success. 
	//   If a child returns Running -> Parallel returns Running until a Success appears (or all fail).
	// - If succeedOnFirst == false -> returns Success when ALL children return Success.
	//   If any child returns Failure -> Parallel returns Failure immediately.
	public class Parallel : ITickable
	{
		private readonly ITickable[] children;
		private readonly bool succeedOnFirst;

		public Parallel(bool succeedOnFirst, params ITickable[] children)
		{
			this.children = children;
			this.succeedOnFirst = succeedOnFirst;
		}

		public Status Tick()
		{
			bool anyRunning = false;
			bool allSuccess = true;

			foreach (var c in children)
			{
				var s = c.Tick();
				if (s == Status.Running) anyRunning = true;
				if (s == Status.Failure)
				{
					if (!succeedOnFirst)
					{
						Reset();
						return Status.Failure;
					}
					// if succeedOnFirst, a failure doesn't immediately cause final failure - keep checking others
					allSuccess = false;
				}
				if (s == Status.Success)
				{
					if (succeedOnFirst)
					{
						Reset();
						return Status.Success;
					}
				}
				if (s != Status.Success) allSuccess = false;
			}

			if (succeedOnFirst)
			{
				// no child succeeded yet; if anything running -> Running, otherwise Failure
				return anyRunning ? Status.Running : Status.Failure;
			}
			else
			{
				// must wait for all success
				if (allSuccess)
				{
					Reset();
					return Status.Success;
				}
				return anyRunning ? Status.Running : Status.Failure;
			}
		}

		public void Reset() { foreach (var c in children) c.Reset(); }
	}

	// --------------------
	// Leaves
	// --------------------
	// Action node: two constructors allowed: Action (instant) or Func<Status> (can return Running).
	public class ActionNode : ITickable
	{
		private readonly Func<Status> actionStatus;

		public ActionNode(Func<Status> actionStatus)
		{
			this.actionStatus = actionStatus;
		}

		public Status Tick() => actionStatus();
		public void Reset() {}
	}

	// Condition node: returns Success if condition true, Failure otherwise
	public class ConditionNode : ITickable
	{
		private readonly Func<bool> condition;
		public ConditionNode(Func<bool> condition) => this.condition = condition;
		public Status Tick() => condition() ? Status.Success : Status.Failure;
		public void Reset() { }
	}

	// Wait node: returns Running until the duration has passed, then Success
	public class WaitNode : ITickable
	{
		private readonly float duration;
		private float startTime;
		private bool started;

		public WaitNode(float seconds) => duration = seconds;

		public Status Tick()
		{
			if (!started)
			{
				startTime = Time.time;
				started = true;
			}

			if (Time.time - startTime >= duration) return Status.Success;
			return Status.Running;
		}

		public void Reset() => started = false;
	}

	// UtilitySelector: pick highest scored action (instant). Returns Success.
	public class UtilitySelector : ITickable
	{
		private readonly List<(Func<float> score, Action action)> actions;

		public UtilitySelector(params (Func<float>, Action)[] actions)
		{
			this.actions = new List<(Func<float>, Action)>(actions);
		}

		public Status Tick()
		{
			if (actions.Count == 0) return Status.Success;
			float best = float.MinValue;
			Action bestAction = null;
			foreach (var (score, action) in actions)
			{
				var s = score();
				if (s > best) { best = s; bestAction = action; }
			}
			bestAction?.Invoke();
			return Status.Success;
		}

		public void Reset() { }
	}

	// --------------------
	// Decorators
	// --------------------
	// Inverter: Success <-> Failure, Running passes through
	public class Inverter : ITickable
	{
		private readonly ITickable child;
		public Inverter(ITickable child) => this.child = child;

		public Status Tick()
		{
			var s = child.Tick();
			if (s == Status.Running) return Status.Running;
			if (s == Status.Success) return Status.Failure;
			return Status.Success; // s == Failure
		}

		public void Reset() => child.Reset();
	}

	// Repeater: repeats child infinitely. If child returns Running -> propagate Running.
	// If child returns Success or Failure -> reset child and keep repeating (return Running).
	public class Repeater : ITickable
	{
		private readonly ITickable child;
		public Repeater(ITickable child) => this.child = child;

		public Status Tick()
		{
			var s = child.Tick();
			if (s == Status.Running) return Status.Running;

			// child finished -> reset so it can run again next tick and keep repeating
			child.Reset();
			return Status.Running;
		}

		public void Reset() => child.Reset();
	}

	// RepeatUntil (repeat until child == Success)  OR repeat until child == Failure if untilSuccess == false
	public class RepeatUntil : ITickable
	{
		private readonly ITickable child;
		private readonly bool untilSuccess;

		public RepeatUntil(ITickable child, bool untilSuccess = true)
		{
			this.child = child;
			this.untilSuccess = untilSuccess;
		}

		public Status Tick()
		{
			var s = child.Tick();

			if (s == Status.Running) return Status.Running;

			if (untilSuccess)
			{
				if (s == Status.Success)
				{
					child.Reset();
					return Status.Success;
				}
				else // Failure -> reset and keep trying
				{
					child.Reset();
					return Status.Running;
				}
			}
			else // untilFail
			{
				if (s == Status.Failure)
				{
					child.Reset();
					return Status.Success;
				}
				else // Success -> reset and keep trying
				{
					child.Reset();
					return Status.Running;
				}
			}
		}

		public void Reset() => child.Reset();
	}

	// Cooldown: if on cooldown -> Running; otherwise ticks child. Success updates lastTime.
	public class Cooldown : ITickable
	{
		private readonly ITickable child;
		private readonly float cooldown;
		private float lastTime = -999f;

		public Cooldown(ITickable child, float cooldown)
		{
			this.child = child;
			this.cooldown = cooldown;
		}

		public Status Tick()
		{
			if (Time.time - lastTime < cooldown) return Status.Running;

			var s = child.Tick();
			if (s == Status.Running) return Status.Running;
			if (s == Status.Success)
			{
				lastTime = Time.time;
				return Status.Success;
			}
			return Status.Failure;
		}

		public void Reset() => child.Reset();
	}

	// Limiter: allow child to succeed up to `limit` times, then fail subsequently.
	public class Limiter : ITickable
	{
		private readonly ITickable child;
		private readonly int limit;
		private int counter = 0;

		public Limiter(ITickable child, int limit)
		{
			this.child = child;
			this.limit = limit;
		}

		public Status Tick()
		{
			if (counter >= limit) return Status.Failure; // blocked

			var s = child.Tick();
			if (s == Status.Running) return Status.Running;
			if (s == Status.Success)
			{
				counter++;
				return Status.Success;
			}
			return Status.Failure;
		}

		public void Reset()
		{
			counter = 0;
			child.Reset();
		}
	}

	// --------------------
	// DSL Factory
	// --------------------
	public static class BT
	{
		// Composites
		public static ITickable Seq(params ITickable[] nodes) => new Sequence(nodes);
		public static ITickable Sel(params ITickable[] nodes) => new Selector(nodes);
		public static ITickable PrioritySel(params ITickable[] nodes) => new PrioritySelector(nodes);
		public static ITickable Par(bool succeedOnFirst, params ITickable[] nodes) => new Parallel(succeedOnFirst, nodes);

		// Leaves
		public static ITickable Act(Func<Status> fn) => new ActionNode(fn);
		public static ITickable Cond(Func<bool> fn) => new ConditionNode(fn);
		public static ITickable Wait(float seconds) => new WaitNode(seconds);

		// Utility
		public static ITickable Util(params (Func<float>, Action)[] evals) => new UtilitySelector(evals);

		// Decorators
		public static ITickable Invert(ITickable node) => new Inverter(node);
		public static ITickable Repeat(ITickable node) => new Repeater(node);
		public static ITickable UntilSuccess(ITickable node) => new RepeatUntil(node, true);
		public static ITickable UntilFail(ITickable node) => new RepeatUntil(node, false);
		public static ITickable Cool(ITickable node, float sec) => new Cooldown(node, sec);
		public static ITickable Limit(ITickable node, int times) => new Limiter(node, times);
	}

	// --------------------
	// Utility DSL
	// --------------------
	public static class UtilDSL
	{
		public static (Func<float>, Action) Act(
			string name,
			float[] factors,
			float[] weights,
			Action action)
		{
			return (() =>
			{
				return UtilityCalculator.CalculateUtility(factors, weights, factors.Length);
			},
			() =>
			{
				Debug.Log($"[Utility] Chosen: {name}");
				action();
			});
		}
	}
}}
