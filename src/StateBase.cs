namespace GirlsDevGames.KatyaAi {
public class StateBase<T>
{
	private string class_name;
	protected T owner;
	
	private short method_id = -1;
	
	// User-specified callbacks
	private System.Func<bool> EnterCallback  { get; set; } = null;
	private System.Action     UpdateCallback { get; set; } = null;
	private System.Func<bool> ExitCallback   { get; set; } = null;

	// Constructor
	public StateBase(
		T owner = default,
		string stateName = "",
		System.Action update = null,
		System.Func<bool> enter = null,
		System.Func<bool> exit = null,
		System.Func<bool> validate = null) {

		class_name = stateName == "" ? this.GetType().Name : stateName;
		this.owner = owner;
		
		EnterCallback  = enter;
		UpdateCallback = update;
		ExitCallback   = exit;

		Reset();
	}

	public virtual void Reset() {}

	public virtual void OnSwitch(short method_id)
	{
		this.method_id = method_id; 
	}
	
	// Default implementations of Enter, Update, and Exit methods
	public virtual bool Enter()
	{
		if (EnterCallback != null) {
			
			if (this.method_id != StateMachine<T>.ENTER_FUNC)
				OnSwitch(StateMachine<T>.ENTER_FUNC);
	
			return EnterCallback();
		}
		
		return true;
	}
	
	public virtual void Update() 
	{
		if (UpdateCallback != null) {
			
			if (this.method_id != StateMachine<T>.UPDATE_FUNC)
				OnSwitch(StateMachine<T>.UPDATE_FUNC);
			
			UpdateCallback();
		}
	}
	
	public virtual bool Exit()
	{
		if (ExitCallback != null) {
			
			if (this.method_id != StateMachine<T>.EXIT_FUNC)
				OnSwitch(StateMachine<T>.EXIT_FUNC);
			
			return ExitCallback();
		}
		
		return true;
	}
	
	// setters and getters
	public void SetOwner(T owner) { this.owner = owner; }
	
	public T Owner() { return this.owner; }
	public string Name() { return class_name; }
	public short MethodID() { return method_id; }
	public override string ToString() { return class_name; }
	
	public bool HasEnterCallback()
	{ return EnterCallback != null; }
	
	public bool HasUpdateCallback()
	{ return UpdateCallback != null; }
	
	public bool HasExitCallback()
	{ return ExitCallback != null; }
}}
