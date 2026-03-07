using System.Globalization;
using System.Reflection;

namespace Sandbox.UI.Dev;

public class ConsoleTextEntry : TextEntry
{
	struct SuggestionItem
	{
		public string Title;
		public string Value;
		public string Command;
		public string CurrentValue;
	}

	public Func<string, object[]> SuggestionProvider { get; set; }

	Panel suggestionPanel;
	Panel smartArgPanel;
	Panel smartArgControls;
	Label smartArgCommandLabel;
	string smartArgControlCommand;
	SmartArgControlMode smartArgControlMode;

	readonly List<SuggestionItem> suggestionItems = new();
	int selectedSuggestionIndex = -1;
	bool preserveSuggestionListOnce;

	public ConsoleTextEntry()
	{
		AddClass( "console-textentry" );
	}

	public override void OnButtonEvent( ButtonEvent e )
	{
		if ( e.Button == "tab" )
		{
			e.StopPropagation = true;
			return;
		}

		base.OnButtonEvent( e );
	}

	public override void OnButtonTyped( ButtonEvent e )
	{
		var button = e.Button;

		if ( button == "tab" )
		{
			var parsed = ParseInput( Text );
			if ( parsed.HasArgumentSlot && ResolveCommandInfo( parsed.Command ).Exists )
			{
				e.StopPropagation = true;
				return;
			}

			if ( e.HasShift )
			{
				e.StopPropagation = true;
				return;
			}

			if ( !suggestionPanel.IsValid() )
				UpdateSuggestionPopup();

			if ( suggestionPanel.IsValid() && suggestionItems.Count > 0 )
			{
				e.StopPropagation = true;
				MoveSuggestionSelection( 1 );
				return;
			}
		}

		if ( button == "up" || button == "down" )
		{
			if ( suggestionPanel.IsValid() && suggestionItems.Count > 0 )
			{
				e.StopPropagation = true;
				MoveSuggestionSelection( button == "up" ? -1 : 1 );
				return;
			}

			e.StopPropagation = true;
			return;
		}

		if ( button == "escape" && suggestionPanel.IsValid() )
		{
			e.StopPropagation = true;
			DestroySuggestionPopup();
			return;
		}

		base.OnButtonTyped( e );
	}

	public override void OnValueChanged()
	{
		base.OnValueChanged();
		UpdateInteractivePanels();
	}

	protected override void OnFocus( PanelEvent e )
	{
		base.OnFocus( e );
		UpdateInteractivePanels();
	}

	protected override void OnBlur( PanelEvent e )
	{
		base.OnBlur( e );
		DestroySuggestionPopup();
		DestroyCommandPanel();
	}

	public void FocusInput() => Focus();

	public void ClearInput()
	{
		Text = string.Empty;
		CaretPosition = 0;
		DestroySuggestionPopup();
		DestroyCommandPanel();
		OnValueChanged();
		StateHasChanged();
	}

	public object[] FillAutoComplete( string arg )
	{
		if ( string.IsNullOrWhiteSpace( arg ) )
			return Array.Empty<string>();

		var trimmed = arg.TrimStart();
		var firstSeparator = trimmed.IndexOf( ' ' );
		var firstToken = firstSeparator < 0 ? trimmed : trimmed[..firstSeparator];
		var hasSecondArgumentInput = firstSeparator >= 0 && !string.IsNullOrWhiteSpace( trimmed[(firstSeparator + 1)..] );
		var query = hasSecondArgumentInput ? arg : firstToken;

		var entries = MenuUtility.AutoComplete( query, 20 )
			.GroupBy( x => x.Command, StringComparer.OrdinalIgnoreCase )
			.Select( x => x.First() )
			.OrderByDescending( x => string.Equals( x.Command, firstToken, StringComparison.OrdinalIgnoreCase ) )
			.ThenByDescending( x => x.Command.StartsWith( firstToken, StringComparison.OrdinalIgnoreCase ) )
			.ThenBy( x => x.Command )
			.ToList();

		return entries.Select( x => (object)new TextEntry.AutocompleteEntry
		{
			Title = x.Command,
			Value = x.Command
		} ).ToArray();
	}

	void UpdateSuggestionPopup()
	{
		if ( SuggestionProvider == null )
		{
			DestroySuggestionPopup();
			return;
		}

		var options = SuggestionProvider( Text ) ?? Array.Empty<object>();
		if ( options.Length == 0 )
		{
			if ( preserveSuggestionListOnce && suggestionPanel.IsValid() && suggestionItems.Count > 0 )
			{
				preserveSuggestionListOnce = false;
				UpdateSelectionClasses();
				UpdateTypedMatchClasses();
				return;
			}

			preserveSuggestionListOnce = false;
			DestroySuggestionPopup();
			return;
		}

		preserveSuggestionListOnce = false;

		if ( !suggestionPanel.IsValid() || suggestionPanel.IsDeleting )
		{
			suggestionPanel = Add.Panel( "console-suggestion-menu" );
			suggestionPanel.SetClass( "hidden", true );
		}
		suggestionPanel.SetClass( "hidden", false );
		suggestionPanel.ScrollOffset = 0;

		suggestionPanel.DeleteChildren( true );
		suggestionItems.Clear();

		foreach ( var option in options )
		{
			var item = ConvertOption( option );
			if ( string.IsNullOrWhiteSpace( item.Value ) )
				continue;

			item.Command = ExtractFirstToken( item.Value );
			item.CurrentValue = string.IsNullOrWhiteSpace( item.Command ) ? null : ConsoleSystem.GetValue( item.Command );
			suggestionItems.Add( item );

			var suggestionValue = item.Value;
			var row = suggestionPanel.Add.Panel( "suggestion-item" );
			row.AddChild( new Label( item.Title, "name" ) );
			row.AddChild( new Label( item.CurrentValue ?? string.Empty, "value" ) );
			row.AddEventListener( "onclick", () => ApplySuggestion( suggestionValue ) );
			row.UserData = item;
		}

		if ( suggestionItems.Count == 0 )
		{
			DestroySuggestionPopup();
			return;
		}

		selectedSuggestionIndex = suggestionItems.FindIndex( x => string.Equals( x.Value, Text, StringComparison.OrdinalIgnoreCase ) );
		UpdateSelectionClasses();
		UpdateTypedMatchClasses();
	}

	void DestroySuggestionPopup()
	{
		suggestionPanel?.SetClass( "hidden", true );
		suggestionItems.Clear();
		selectedSuggestionIndex = -1;
	}

	static SuggestionItem ConvertOption( object option )
	{
		if ( option is TextEntry.AutocompleteEntry entry )
		{
			var value = entry.Value?.ToString() ?? string.Empty;
			return new SuggestionItem
			{
				Title = string.IsNullOrWhiteSpace( entry.Title ) ? value : entry.Title,
				Value = value
			};
		}

		var raw = option?.ToString() ?? string.Empty;
		return new SuggestionItem
		{
			Title = raw,
			Value = raw
		};
	}

	void MoveSuggestionSelection( int direction )
	{
		if ( suggestionItems.Count == 0 )
			return;

		if ( selectedSuggestionIndex < 0 )
			selectedSuggestionIndex = direction > 0 ? 0 : suggestionItems.Count - 1;
		else
			selectedSuggestionIndex = (selectedSuggestionIndex + direction + suggestionItems.Count) % suggestionItems.Count;

		UpdateSelectionClasses();
		ApplySuggestion( suggestionItems[selectedSuggestionIndex].Value, false );
	}

	void ApplySuggestion( string value )
	{
		ApplySuggestion( value, true );
	}

	void ApplySuggestion( string value, bool refreshSuggestions )
	{
		Text = value;
		CaretPosition = TextLength;

		if ( refreshSuggestions )
		{
			OnValueChanged();
		}
		else
		{
			preserveSuggestionListOnce = true;
			UpdateTypedMatchClasses();
		}

		Focus();
	}

	void UpdateInteractivePanels()
	{
		var parsed = ParseInput( Text );
		var hasCommandPanelContext = parsed.HasArgumentSlot && !string.IsNullOrWhiteSpace( parsed.Command );
		var model = hasCommandPanelContext ? BuildCommandPanelModel( parsed ) : default;
		var isCommandPanelMode = model.IsValid;

		if ( isCommandPanelMode )
		{
			DestroySuggestionPopup();
			UpdateCommandPanel( model );
			return;
		}

		DestroyCommandPanel();
		UpdateSuggestionPopup();
	}

	void UpdateCommandPanel( SmartArgumentModel model )
	{
		if ( !model.IsValid )
		{
			DestroyCommandPanel();
			return;
		}

		EnsureCommandPanel();
		smartArgPanel.SetClass( "hidden", false );
		smartArgCommandLabel.Text = model.Command;
		smartArgPanel.SetClass( "is-bool", model.IsBoolean );
		smartArgPanel.SetClass( "has-actions", model.IsBoolean || model.IsNumeric );

		var targetMode = model.IsBoolean ? SmartArgControlMode.Boolean
			: model.IsNumeric ? SmartArgControlMode.Numeric
			: SmartArgControlMode.None;

		var sameCommand = string.Equals( smartArgControlCommand, model.Command, StringComparison.OrdinalIgnoreCase );
		var needsRebuild = !smartArgControls.IsValid() || !sameCommand || smartArgControlMode != targetMode;

		if ( needsRebuild )
		{
			RebuildCommandControls( model, targetMode );
		}
		else if ( targetMode == SmartArgControlMode.Boolean )
		{
			UpdateBooleanControlState( model );
		}
	}

	void EnsureCommandPanel()
	{
		if ( smartArgPanel.IsValid() && !smartArgPanel.IsDeleting )
			return;

		smartArgPanel = Add.Panel( "command-panel" );
		smartArgPanel.SetClass( "hidden", true );
		smartArgPanel.AddEventListener( "onmousedown", ( PanelEvent _ ) => Focus() );
		var header = smartArgPanel.Add.Panel( "header" );
		smartArgCommandLabel = header.AddChild( new Label( string.Empty, "command" ) );

		smartArgControls = smartArgPanel.Add.Panel( "controls" );
	}

	void DestroyCommandPanel()
	{
		if ( !smartArgPanel.IsValid() )
			return;

		smartArgPanel.SetClass( "hidden", true );
		smartArgControls?.DeleteChildren( true );
		smartArgControlCommand = null;
		smartArgControlMode = SmartArgControlMode.None;
	}

	void RebuildCommandControls( SmartArgumentModel model, SmartArgControlMode mode )
	{
		if ( !smartArgControls.IsValid() )
			return;

		smartArgControls.DeleteChildren( true );

		if ( mode == SmartArgControlMode.Boolean )
		{
			BuildBooleanControls( model );
		}
		else if ( mode == SmartArgControlMode.Numeric )
		{
			BuildNumericControls( model );
		}

		smartArgControlCommand = model.Command;
		smartArgControlMode = mode;
	}

	void UpdateBooleanControlState( SmartArgumentModel model )
	{
		if ( !smartArgControls.IsValid() )
			return;

		var children = smartArgControls.Children.ToArray();
		if ( children.Length < 2 )
			return;

		var hasExplicitTypedBool = model.HasTypedValue && model.HasTypedBool;
		var effectiveBool = hasExplicitTypedBool ? model.TypedBool : model.CurrentBool;

		var offButton = children[0];
		var onButton = children[1];
		offButton?.SetClass( "active", hasExplicitTypedBool && !effectiveBool );
		onButton?.SetClass( "active", hasExplicitTypedBool && effectiveBool );
	}

	void BuildBooleanControls( SmartArgumentModel model )
	{
		var offButton = AddSmartArgButton( "OFF", () => SetInputArgumentValue( model.Command, model.FalseToken ) );
		var onButton = AddSmartArgButton( "ON", () => SetInputArgumentValue( model.Command, model.TrueToken ) );
		UpdateBooleanControlState( model );
	}

	void BuildNumericControls( SmartArgumentModel model )
	{
		var effectiveModel = ResolveLatestNumericModel( model );
		var decimals = effectiveModel.DecimalPlaces;
		var smallStep = decimals > 0 ? 0.1 : 1.0;
		var largeStep = smallStep * 10.0;

		AddSmartArgButton( $"-{FormatStep( largeStep )}", () => ShiftNumericArgument( model, -largeStep ) );
		AddSmartArgButton( $"-{FormatStep( smallStep )}", () => ShiftNumericArgument( model, -smallStep ) );
		var currentButton = AddSmartArgButton( "current", () =>
		{
			var currentValue = ConsoleSystem.GetValue( model.Command, model.CurrentValue ) ?? model.CurrentValue;
			SetInputArgumentValue( model.Command, currentValue );
		} );
		currentButton?.AddClass( "secondary" );
		AddSmartArgButton( $"+{FormatStep( smallStep )}", () => ShiftNumericArgument( model, smallStep ) );
		AddSmartArgButton( $"+{FormatStep( largeStep )}", () => ShiftNumericArgument( model, largeStep ) );
	}

	void ShiftNumericArgument( SmartArgumentModel model, double delta )
	{
		var effectiveModel = ResolveLatestNumericModel( model );
		var baseValue = effectiveModel.HasTypedNumber ? effectiveModel.TypedNumber : effectiveModel.CurrentNumber;
		var result = baseValue + delta;

		if ( effectiveModel.MinValue.HasValue )
			result = Math.Max( result, effectiveModel.MinValue.Value );
		if ( effectiveModel.MaxValue.HasValue )
			result = Math.Min( result, effectiveModel.MaxValue.Value );

		var formatted = FormatNumber( result, effectiveModel.DecimalPlaces );
		SetInputArgumentValue( effectiveModel.Command, formatted );
	}

	SmartArgumentModel ResolveLatestNumericModel( SmartArgumentModel fallback )
	{
		var parsed = ParseInput( Text );
		if ( !parsed.HasArgumentSlot || !string.Equals( parsed.Command, fallback.Command, StringComparison.OrdinalIgnoreCase ) )
			return fallback;

		var latest = BuildCommandPanelModel( parsed );
		return latest.IsValid && latest.IsNumeric ? latest : fallback;
	}

	Button AddSmartArgButton( string text, Action onClick )
	{
		if ( !smartArgControls.IsValid() )
			return null;

		var button = smartArgControls.AddChild( new Button( text, onClick ) );
		button.AddClass( "arg-btn" );
		return button;
	}

	void SetInputArgumentValue( string command, string rawValue )
	{
		if ( string.IsNullOrWhiteSpace( command ) )
			return;

		var commandToken = command.Trim();
		var valueToken = PrepareArgumentToken( rawValue );
		Text = string.IsNullOrWhiteSpace( valueToken ) ? commandToken : $"{commandToken} {valueToken}";
		CaretPosition = TextLength;
		Focus();
	}

	static string PrepareArgumentToken( string value )
	{
		var text = value?.Trim() ?? string.Empty;
		if ( string.IsNullOrWhiteSpace( text ) )
			return string.Empty;

		if ( text.Contains( ' ' ) && !(text.StartsWith( '"' ) && text.EndsWith( '"' )) )
		{
			return $"\"{text.Replace( "\"", "\\\"" )}\"";
		}

		return text;
	}

	static string FormatStep( double step )
	{
		if ( Math.Abs( step % 1d ) < 0.000001d )
			return ((int)Math.Round( step )).ToString( CultureInfo.InvariantCulture );

		return step.ToString( "0.##", CultureInfo.InvariantCulture );
	}

	static string FormatNumber( double value, int decimals )
	{
		if ( decimals <= 0 )
			return Math.Round( value ).ToString( CultureInfo.InvariantCulture );

		var clampedDecimals = Math.Clamp( decimals, 1, 4 );
		var format = $"0.{new string( '#', clampedDecimals )}";
		return value.ToString( format, CultureInfo.InvariantCulture );
	}

	SmartArgumentModel BuildCommandPanelModel( ParsedInput parsed )
	{
		var commandInfo = ResolveCommandInfo( parsed.Command );
		if ( !commandInfo.Exists )
			return default;

		var currentValue = commandInfo.IsVariable ? commandInfo.CurrentValue : null;

		var model = new SmartArgumentModel
		{
			IsValid = true,
			Command = parsed.Command,
			CurrentValue = string.IsNullOrWhiteSpace( currentValue ) ? "-" : currentValue,
			TypedValue = parsed.ArgumentText,
			HasTypedValue = parsed.HasArgument,
			MinValue = commandInfo.MinValue,
			MaxValue = commandInfo.MaxValue
		};

		if ( !commandInfo.IsVariable || string.IsNullOrWhiteSpace( currentValue ) )
			return model;

		if ( TryParseBoolToken( currentValue, out var currentBool ) )
		{
			model.IsBoolean = true;
			model.CurrentBool = currentBool;
			model.TrueToken = UsesNumericBooleanToken( currentValue ) ? "1" : "true";
			model.FalseToken = UsesNumericBooleanToken( currentValue ) ? "0" : "false";

			if ( parsed.HasArgument && TryParseBoolToken( parsed.ArgumentText, out var typedBool ) )
			{
				model.HasTypedBool = true;
				model.TypedBool = typedBool;
			}

			return model;
		}

		if ( TryParseNumberToken( currentValue, out var currentNumber ) )
		{
			model.IsNumeric = true;
			model.CurrentNumber = currentNumber;
			model.DecimalPlaces = Math.Max( 0, CountDecimalPlaces( currentValue ) );

			if ( parsed.HasArgument && TryParseNumberToken( parsed.ArgumentText, out var typedNumber ) )
			{
				model.HasTypedNumber = true;
				model.TypedNumber = typedNumber;
				model.DecimalPlaces = Math.Max( model.DecimalPlaces, CountDecimalPlaces( parsed.ArgumentText ) );
			}

			return model;
		}

		return model;
	}

	static bool TryParseNumberToken( string value, out double number )
	{
		return double.TryParse( value, NumberStyles.Float, CultureInfo.InvariantCulture, out number )
			|| double.TryParse( value, NumberStyles.Float, CultureInfo.CurrentCulture, out number );
	}

	static bool TryParseBoolToken( string value, out bool result )
	{
		result = false;
		if ( string.IsNullOrWhiteSpace( value ) )
			return false;

		var raw = value.Trim();
		if ( bool.TryParse( raw, out result ) )
			return true;

		switch ( raw.ToLowerInvariant() )
		{
			case "1":
			case "on":
			case "yes":
				result = true;
				return true;
			case "0":
			case "off":
			case "no":
				result = false;
				return true;
			default:
				return false;
		}
	}

	static bool UsesNumericBooleanToken( string value )
	{
		var raw = value?.Trim();
		return string.Equals( raw, "0", StringComparison.OrdinalIgnoreCase )
			|| string.Equals( raw, "1", StringComparison.OrdinalIgnoreCase );
	}

	static int CountDecimalPlaces( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
			return 0;

		var raw = value.Trim();
		var dot = raw.IndexOf( '.' );
		if ( dot < 0 )
			return 0;

		var count = 0;
		for ( var i = dot + 1; i < raw.Length; i++ )
		{
			if ( !char.IsDigit( raw[i] ) )
				break;
			count++;
		}

		return count;
	}

	static ParsedInput ParseInput( string text )
	{
		if ( string.IsNullOrWhiteSpace( text ) )
			return default;

		var trimmed = text.TrimStart();
		if ( string.IsNullOrWhiteSpace( trimmed ) )
			return default;

		var separator = trimmed.IndexOf( ' ' );
		if ( separator < 0 )
		{
			return new ParsedInput
			{
				Command = trimmed,
				HasArgumentSlot = false
			};
		}

		var command = trimmed[..separator];
		var argument = trimmed[(separator + 1)..];
		var hasArgument = !string.IsNullOrWhiteSpace( argument );

		return new ParsedInput
		{
			Command = command,
			ArgumentText = hasArgument ? argument.Trim() : string.Empty,
			HasArgument = hasArgument,
			HasArgumentSlot = true
		};
	}

	static readonly Type ConVarSystemType = typeof( ConsoleSystem ).Assembly.GetType( "Sandbox.ConVarSystem" );
	static readonly MethodInfo ConVarFindMethod = ConVarSystemType?.GetMethod( "Find", BindingFlags.Static | BindingFlags.NonPublic );

	static CommandInfo ResolveCommandInfo( string command )
	{
		if ( string.IsNullOrWhiteSpace( command ) || ConVarFindMethod is null )
			return default;

		try
		{
			var instance = ConVarFindMethod.Invoke( null, new object[] { command } );
			if ( instance is null )
				return default;

			return new CommandInfo
			{
				Exists = true,
				IsVariable = ReadBoolProperty( instance, "IsVariable" ),
				CurrentValue = ConsoleSystem.GetValue( command, null ),
				MinValue = ReadNullableFloatProperty( instance, "MinValue" ),
				MaxValue = ReadNullableFloatProperty( instance, "MaxValue" )
			};
		}
		catch
		{
			return default;
		}
	}

	static bool ReadBoolProperty( object instance, string property )
	{
		var value = instance.GetType().GetProperty( property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )?.GetValue( instance );
		return value is bool b && b;
	}

	static float? ReadNullableFloatProperty( object instance, string property )
	{
		var value = instance.GetType().GetProperty( property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )?.GetValue( instance );
		if ( value is float f )
			return f;

		return value as float?;
	}

	struct ParsedInput
	{
		public string Command;
		public string ArgumentText;
		public bool HasArgument;
		public bool HasArgumentSlot;
	}

	struct CommandInfo
	{
		public bool Exists;
		public bool IsVariable;
		public string CurrentValue;
		public float? MinValue;
		public float? MaxValue;
	}

	struct SmartArgumentModel
	{
		public bool IsValid;
		public string Command;
		public string CurrentValue;
		public string TypedValue;
		public bool HasTypedValue;

		public bool IsBoolean;
		public bool CurrentBool;
		public bool HasTypedBool;
		public bool TypedBool;
		public string TrueToken;
		public string FalseToken;

		public bool IsNumeric;
		public double CurrentNumber;
		public bool HasTypedNumber;
		public double TypedNumber;
		public int DecimalPlaces;
		public float? MinValue;
		public float? MaxValue;
	}

	enum SmartArgControlMode
	{
		None,
		Boolean,
		Numeric
	}

	void UpdateSelectionClasses()
	{
		if ( !suggestionPanel.IsValid() )
			return;

		var children = suggestionPanel.Children.ToArray();
		for ( var i = 0; i < children.Length; i++ )
		{
			children[i]?.SetClass( "active", i == selectedSuggestionIndex );
		}
	}

	void UpdateTypedMatchClasses()
	{
		if ( !suggestionPanel.IsValid() )
			return;

		var typedCommand = ExtractFirstToken( Text );
		var children = suggestionPanel.Children.ToArray();
		for ( var i = 0; i < children.Length; i++ )
		{
			var item = i < suggestionItems.Count ? suggestionItems[i] : default;
			var isTypedMatch = !string.IsNullOrWhiteSpace( typedCommand )
				&& string.Equals( typedCommand, item.Command, StringComparison.OrdinalIgnoreCase );

			children[i]?.SetClass( "typed-match", isTypedMatch );
		}
	}

	static string ExtractFirstToken( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
			return string.Empty;

		var trimmed = value.TrimStart();
		var separator = trimmed.IndexOf( ' ' );
		return separator < 0 ? trimmed : trimmed[..separator];
	}
}
