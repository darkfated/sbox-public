namespace Sandbox.UI.Dev;

public class ConsoleInput : TextEntry
{
	const int MaxVisibleSuggestions = 32;

	struct SuggestionItem
	{
		public string Title;
		public string Value;
		public string Command;
	}

	public Func<string, object[]> SuggestionProvider { get; set; }

	readonly SuggestionPopup suggestions;
	readonly CommandPanel commandPanel;

	RealTimeUntil? hideAt;

	public ConsoleInput()
	{
		suggestions = new SuggestionPopup( this );
		commandPanel = new CommandPanel( this );
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
		if ( e.Button == "tab" )
		{
			var parsed = ParseInput( Text );
			if ( (parsed.ArgumentText != null && commandPanel.BuildModelAndCheckValid( parsed ).IsValid) || e.HasShift )
			{
				e.StopPropagation = true;
				return;
			}

			if ( !suggestions.IsValid )
				suggestions.Update( Text, SuggestionProvider );

			if ( suggestions.HasItems )
			{
				e.StopPropagation = true;
				suggestions.MoveSelection( 1 );
				return;
			}
		}

		if ( e.Button == "up" || e.Button == "down" )
		{
			e.StopPropagation = true;
			if ( suggestions.HasItems )
				suggestions.MoveSelection( e.Button == "up" ? -1 : 1 );
			return;
		}

		if ( e.Button == "escape" && suggestions.IsValid )
		{
			e.StopPropagation = true;
			suggestions.Destroy();
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
		hideAt = null;
		UpdateInteractivePanels();
	}

	protected override void OnBlur( PanelEvent e )
	{
		base.OnBlur( e );
		hideAt = 0.15f;
	}

	public override void Tick()
	{
		base.Tick();

		if ( hideAt.HasValue && hideAt.Value )
		{
			hideAt = null;

			if ( HasFocus || suggestions.HasFocusInside || commandPanel.HasFocusInside )
				return;

			suggestions.Destroy();
			commandPanel.Destroy();
		}
	}

	public void FocusInput() => Focus();

	public void ClearInput()
	{
		Text = string.Empty;
		CaretPosition = 0;
		suggestions.Destroy();
		commandPanel.Destroy();
		OnValueChanged();
		StateHasChanged();
	}

	public object[] FillAutoComplete( string arg )
	{
		if ( string.IsNullOrWhiteSpace( arg ) )
			return Array.Empty<object>();

		var trimmed = arg.TrimStart();
		var firstSeparator = trimmed.IndexOf( ' ' );
		var firstToken = firstSeparator < 0 ? trimmed : trimmed[..firstSeparator];
		var hasSecondArgumentInput = firstSeparator >= 0 && !string.IsNullOrWhiteSpace( trimmed[(firstSeparator + 1)..] );
		var query = hasSecondArgumentInput ? arg : firstToken;

		return MenuUtility.AutoComplete( query, 20 )
			.GroupBy( x => x.Command, StringComparer.OrdinalIgnoreCase )
			.Select( x => x.First() )
			.OrderByDescending( x => string.Equals( x.Command, firstToken, StringComparison.OrdinalIgnoreCase ) )
			.ThenByDescending( x => x.Command.StartsWith( firstToken, StringComparison.OrdinalIgnoreCase ) )
			.ThenBy( x => x.Command )
			.Select( x => (object)new TextEntry.AutocompleteEntry
			{
				Title = x.Command,
				Value = x.Command
			} )
			.ToArray();
	}

	void UpdateInteractivePanels()
	{
		var parsed = ParseInput( Text );
		var model = parsed.ArgumentText != null
			? commandPanel.BuildModelAndCheckValid( parsed )
			: default;
		if ( model.IsValid )
		{
			suggestions.Destroy();
			commandPanel.Update( model );
			return;
		}

		commandPanel.Destroy();
		suggestions.Update( Text, SuggestionProvider );
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

	static string FormatSuggestionValue( string rawValue )
	{
		if ( TryParseBoolToken( rawValue, out var boolValue ) )
			return boolValue ? "True" : "False";

		return rawValue ?? string.Empty;
	}

	static bool TryParseNumberToken( string value, out double number )
	{
		return double.TryParse(
			value,
			System.Globalization.NumberStyles.Float,
			System.Globalization.CultureInfo.InvariantCulture,
			out number );
	}

	static bool TryParseBoolToken( string value, out bool result )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
		{
			result = false;
			return false;
		}

		var raw = value.Trim();
		if ( raw == "1" )
		{
			result = true;
			return true;
		}
		if ( raw == "0" )
		{
			result = false;
			return true;
		}
		result = false;
		return false;
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
		var trimmed = text?.TrimStart();
		if ( string.IsNullOrWhiteSpace( trimmed ) )
			return default;

		var separator = trimmed.IndexOf( ' ' );
		if ( separator < 0 )
		{
			return new ParsedInput
			{
				Command = trimmed
			};
		}

		var argument = trimmed[(separator + 1)..];
		return new ParsedInput
		{
			Command = trimmed[..separator],
			ArgumentText = argument.Trim()
		};
	}

	static string ExtractFirstToken( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
			return string.Empty;

		var trimmed = value.TrimStart();
		var separator = trimmed.IndexOf( ' ' );
		return separator < 0 ? trimmed : trimmed[..separator];
	}

	Panel CreateFloatingSibling( string cssClass )
	{
		var host = Parent ?? this;
		var p = host.AddChild<Panel>();
		p.AddClass( cssClass );
		return p;
	}

	private class SuggestionPopup
	{
		readonly ConsoleInput owner;
		public Panel PanelRef { get; private set; }
		readonly List<SuggestionItem> suggestionItems = new();
		readonly List<Panel> suggestionRows = new();
		int selectedSuggestionIndex = -1;
		bool preserveSuggestionListOnce;

		public bool HasItems => PanelRef.IsValid() && suggestionItems.Count > 0;
		public bool IsValid => PanelRef.IsValid() && !PanelRef.IsDeleting;
		public bool HasFocusInside => PanelRef.IsValid() && PanelRef.HasHovered;

		public SuggestionPopup( ConsoleInput owner )
		{
			this.owner = owner;
		}

		public void Update( string text, Func<string, object[]> provider )
		{
			if ( provider == null )
			{
				Destroy();
				return;
			}

			var options = provider( text ) ?? Array.Empty<object>();
			if ( options.Length == 0 )
			{
				if ( preserveSuggestionListOnce && HasItems )
				{
					preserveSuggestionListOnce = false;
					UpdateSuggestionClasses( updateSelection: true, updateTypedMatch: true );
					return;
				}

				Destroy();
				return;
			}

			preserveSuggestionListOnce = false;

			if ( !IsValid )
			{
				PanelRef = owner.CreateFloatingSibling( "console-suggestion-menu" );
			}

			PanelRef.SetClass( "hidden", true );
			PanelRef.ScrollOffset = 0;
			PanelRef.DeleteChildren();

			suggestionItems.Clear();
			suggestionRows.Clear();
			selectedSuggestionIndex = -1;

			foreach ( var option in options )
			{
				if ( suggestionItems.Count >= MaxVisibleSuggestions )
					break;

				var item = ConvertOption( option );
				if ( string.IsNullOrWhiteSpace( item.Value ) )
					continue;

				item.Command = ExtractFirstToken( item.Value );
				suggestionItems.Add( item );

				var rawCurrentValue = string.IsNullOrWhiteSpace( item.Command )
					? null
					: ConsoleSystem.GetValue( item.Command );

				var rowPanel = PanelRef.Add.Panel( "suggestion-item" );
				rowPanel.AddChild( new Label( item.Title, "name" ) );
				rowPanel.AddChild( new Label( FormatSuggestionValue( rawCurrentValue ), "value" ) );
				rowPanel.UserData = item;
				rowPanel.AddEventListener( "onclick", () =>
				{
					if ( rowPanel.UserData is SuggestionItem selected && !string.IsNullOrWhiteSpace( selected.Value ) )
						ApplySuggestion( selected.Value, true );
				} );
				suggestionRows.Add( rowPanel );

				if ( selectedSuggestionIndex < 0 && string.Equals( item.Value, owner.Text, StringComparison.OrdinalIgnoreCase ) )
					selectedSuggestionIndex = suggestionItems.Count - 1;
			}

			if ( suggestionItems.Count == 0 )
			{
				Destroy();
				return;
			}

			PanelRef.SetClass( "hidden", false );
			UpdateSuggestionClasses( updateSelection: true, updateTypedMatch: true );
			PanelRef.StateHasChanged();
		}

		public void Destroy()
		{
			preserveSuggestionListOnce = false;
			if ( PanelRef.IsValid() )
			{
				PanelRef.Delete();
			}
			PanelRef = null;
			suggestionItems.Clear();
			suggestionRows.Clear();
			selectedSuggestionIndex = -1;
		}

		public void MoveSelection( int direction )
		{
			if ( suggestionItems.Count == 0 )
				return;

			if ( selectedSuggestionIndex < 0 )
				selectedSuggestionIndex = direction > 0 ? 0 : suggestionItems.Count - 1;
			else
				selectedSuggestionIndex = (selectedSuggestionIndex + direction + suggestionItems.Count) % suggestionItems.Count;

			UpdateSuggestionClasses( updateSelection: true, updateTypedMatch: false );
			ApplySuggestion( suggestionItems[selectedSuggestionIndex].Value, false );
		}

		void ApplySuggestion( string value, bool refreshSuggestions )
		{
			owner.Text = value;
			owner.CaretPosition = owner.TextLength;

			if ( refreshSuggestions )
			{
				owner.OnValueChanged();
			}
			else
			{
				preserveSuggestionListOnce = true;
				UpdateSuggestionClasses( updateSelection: false, updateTypedMatch: true );
			}

			owner.Focus();
		}

		void UpdateSuggestionClasses( bool updateSelection, bool updateTypedMatch )
		{
			if ( !PanelRef.IsValid() )
				return;

			var typedCommand = updateTypedMatch ? ExtractFirstToken( owner.Text ) : string.Empty;
			var hasTypedCommand = !string.IsNullOrWhiteSpace( typedCommand );
			var count = Math.Min( suggestionRows.Count, suggestionItems.Count );

			for ( var i = 0; i < count; i++ )
			{
				var row = suggestionRows[i];
				if ( !row.IsValid() )
					continue;

				if ( updateSelection )
				{
					row.SetClass( "active", i == selectedSuggestionIndex );
				}

				if ( !updateTypedMatch )
					continue;

				var item = suggestionItems[i];
				var isTypedMatch = hasTypedCommand
					&& string.Equals( typedCommand, item.Command, StringComparison.OrdinalIgnoreCase );
				row.SetClass( "typed-match", isTypedMatch );
			}
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
	}

	private class CommandPanel
	{
		readonly ConsoleInput owner;
		public Panel PanelRef { get; private set; }
		Label smartArgCommandLabel;
		Panel smartArgControls;

		public bool HasFocusInside => PanelRef.IsValid() && PanelRef.HasHovered;

		public CommandPanel( ConsoleInput owner )
		{
			this.owner = owner;
		}

		public SmartArgumentModel BuildModelAndCheckValid( ParsedInput parsed )
		{
			return BuildCommandPanelModel( parsed );
		}

		public void Update( SmartArgumentModel model )
		{
			EnsureCommandPanel();
			PanelRef.SetClass( "hidden", false );
			smartArgCommandLabel.Text = model.Command;
			PanelRef.SetClass( "is-bool", model.IsBoolean );
			PanelRef.SetClass( "has-actions", model.IsBoolean || model.IsNumeric );
			smartArgControls.DeleteChildren( true );

			if ( model.IsBoolean )
			{
				BuildBooleanControls( model );
			}
			else if ( model.IsNumeric )
			{
				BuildNumericControls( model );
			}

			PanelRef.StateHasChanged();
		}

		void EnsureCommandPanel()
		{
			if ( PanelRef.IsValid() && !PanelRef.IsDeleting )
				return;

			PanelRef = owner.CreateFloatingSibling( "command-panel" );
			PanelRef.SetClass( "hidden", true );
			PanelRef.AddEventListener( "onmousedown", ( PanelEvent _ ) => owner.Focus() );

			var header = PanelRef.Add.Panel( "header" );
			smartArgCommandLabel = header.AddChild( new Label( string.Empty, "command" ) );

			smartArgControls = PanelRef.Add.Panel( "controls" );
		}

		public void Destroy()
		{
			if ( !PanelRef.IsValid() )
				return;

			PanelRef.Delete();
			PanelRef = null;
			smartArgCommandLabel = null;
			smartArgControls = null;
		}

		void BuildBooleanControls( SmartArgumentModel model )
		{
			var offButton = AddSmartArgButton( "OFF", () => owner.SetInputArgumentValue( model.Command, "0" ) );
			var onButton = AddSmartArgButton( "ON", () => owner.SetInputArgumentValue( model.Command, "1" ) );

			if ( !model.HasTypedBool )
				return;

			offButton.SetClass( "active", !model.TypedBool );
			onButton.SetClass( "active", model.TypedBool );
		}

		void BuildNumericControls( SmartArgumentModel model )
		{
			var effectiveModel = ResolveLatestNumericModel( model );
			var decimals = effectiveModel.DecimalPlaces;
			var smallStep = decimals > 0 ? 0.1 : 1.0;
			var largeStep = smallStep * 10.0;
			var smallStepLabel = decimals > 0 ? "0.1" : "1";
			var largeStepLabel = decimals > 0 ? "1" : "10";

			AddSmartArgButton( $"-{largeStepLabel}", () => ShiftNumericArgument( effectiveModel, -largeStep ) );
			AddSmartArgButton( $"-{smallStepLabel}", () => ShiftNumericArgument( effectiveModel, -smallStep ) );

			var currentButton = AddSmartArgButton( "current", () =>
			{
				var currentValue = ConsoleSystem.GetValue( effectiveModel.Command, effectiveModel.CurrentValue ) ?? effectiveModel.CurrentValue;
				owner.SetInputArgumentValue( effectiveModel.Command, currentValue );
			} );
			currentButton.AddClass( "secondary" );

			AddSmartArgButton( $"+{smallStepLabel}", () => ShiftNumericArgument( effectiveModel, smallStep ) );
			AddSmartArgButton( $"+{largeStepLabel}", () => ShiftNumericArgument( effectiveModel, largeStep ) );
		}

		void ShiftNumericArgument( SmartArgumentModel model, double delta )
		{
			var effectiveModel = ResolveLatestNumericModel( model );
			var baseValue = effectiveModel.HasTypedNumber ? effectiveModel.TypedNumber : effectiveModel.CurrentNumber;
			var result = baseValue + delta;
			var decimals = Math.Clamp( effectiveModel.DecimalPlaces, 0, 4 );
			var rounded = Math.Round( result, decimals );
			var formatted = decimals == 0
				? rounded.ToString( System.Globalization.CultureInfo.InvariantCulture )
				: rounded.ToString( $"0.{new string( '#', decimals )}", System.Globalization.CultureInfo.InvariantCulture );
			owner.SetInputArgumentValue( effectiveModel.Command, formatted );
		}

		SmartArgumentModel ResolveLatestNumericModel( SmartArgumentModel fallback )
		{
			var parsed = ParseInput( owner.Text );
			if ( parsed.ArgumentText == null || !string.Equals( parsed.Command, fallback.Command, StringComparison.OrdinalIgnoreCase ) )
				return fallback;

			var latest = BuildCommandPanelModel( parsed );
			return latest.IsValid && latest.IsNumeric ? latest : fallback;
		}

		Button AddSmartArgButton( string text, Action onClick )
		{
			var button = smartArgControls.AddChild( new Button( text, onClick ) );
			button.AddClass( "arg-btn" );
			return button;
		}

		SmartArgumentModel BuildCommandPanelModel( ParsedInput parsed )
		{
			var currentValue = ConsoleSystem.GetValue( parsed.Command, null );
			if ( string.IsNullOrWhiteSpace( currentValue ) )
				return default;

			var hasTypedArgument = !string.IsNullOrWhiteSpace( parsed.ArgumentText );

			var model = new SmartArgumentModel
			{
				IsValid = true,
				Command = parsed.Command,
				CurrentValue = currentValue,
			};

			if ( TryParseBoolToken( currentValue, out _ ) )
			{
				model.IsBoolean = true;

				if ( hasTypedArgument && TryParseBoolToken( parsed.ArgumentText, out var typedBool ) )
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
				model.DecimalPlaces = CountDecimalPlaces( currentValue );

				if ( hasTypedArgument && TryParseNumberToken( parsed.ArgumentText, out var typedNumber ) )
				{
					model.HasTypedNumber = true;
					model.TypedNumber = typedNumber;
					model.DecimalPlaces = Math.Max( model.DecimalPlaces, CountDecimalPlaces( parsed.ArgumentText ) );
				}

				return model;
			}

			return model;
		}
	}

	struct ParsedInput
	{
		public string Command;
		public string ArgumentText;
	}

	struct SmartArgumentModel
	{
		public bool IsValid;
		public string Command;
		public string CurrentValue;

		public bool IsBoolean;
		public bool HasTypedBool;
		public bool TypedBool;

		public bool IsNumeric;
		public double CurrentNumber;
		public bool HasTypedNumber;
		public double TypedNumber;
		public int DecimalPlaces;
	}
}
