namespace Sandbox;

internal static partial class DebugOverlay
{
	public partial class Profiler
	{
		static readonly Dictionary<string, float> _smoothedAvgWidth = new();
		static readonly TextRendering.Outline _outline = new() { Color = Color.Black.WithAlpha( 0.8f ), Size = 2, Enabled = true };

		const float RowHeight = 14f;
		const float NameWidth = 150f;
		const float GaugeWidth = 100f;
		const float ValueWidth = 68f;
		const float GaugeScaleMs = 25f;

		internal static void Draw( ref Vector2 pos )
		{
			var timings = Sandbox.Diagnostics.PerformanceStats.Timings.GetMain().ToArray();
			if ( timings.Length == 0 )
				return;

			var liveNames = timings.Select( x => x.Name ).ToHashSet();
			var stale = _smoothedAvgWidth.Keys.Where( x => !liveNames.Contains( x ) ).ToList();
			foreach ( var key in stale )
				_smoothedAvgWidth.Remove( key );

			var x = pos.x;
			var y = pos.y;
			var colName = x;
			var colGauge = colName + NameWidth + 8;
			var colLast = colGauge + GaugeWidth + 10;
			var colAvg = colLast + ValueWidth;
			var colMax = colAvg + ValueWidth;

			DrawHeader( ref y, x, colLast, colAvg, colMax );

			foreach ( var t in timings.OrderByDescending( t => t.GetMetric( 256 ).Avg ) )
			{
				var last = t.GetMetric( 1 ).Avg;
				var avg = t.GetMetric( 256 ).Avg;
				var max = t.GetMetric( 256 ).Max;

				DrawRow( ref y, t.Name, t.Color, colName, colGauge, colLast, colAvg, colMax, last, avg, max );
			}

			pos.y = y;
		}

		static void DrawHeader( ref float y, float x, float colLast, float colAvg, float colMax )
		{
			var dim = Color.White.WithAlpha( 0.55f );
			DrawTextCell( "name", dim, x, y, NameWidth, TextFlag.LeftCenter );
			DrawTextCell( "last", dim, colLast, y, ValueWidth, TextFlag.LeftCenter );
			DrawTextCell( "avg", dim, colAvg, y, ValueWidth, TextFlag.LeftCenter );
			DrawTextCell( "max", dim, colMax, y, ValueWidth, TextFlag.LeftCenter );

			y += RowHeight;
		}

		static void DrawRow( ref float y, string name, Color color, float colName, float colGauge, float colLast, float colAvg, float colMax, float lastMs, float avgMs, float maxMs )
		{
			DrawTextCell( name, color.Lighten( 0.45f ), colName, y, NameWidth, TextFlag.LeftCenter );

			var gauge = new Rect( colGauge, y + 2, GaugeWidth, RowHeight - 4 );
			Hud.DrawRect( gauge, Color.Black.WithAlpha( 0.2f ), borderWidth: 1, borderColor: Color.White.WithAlpha( 0.08f ) );

			var targetAvgWidth = MathF.Min( gauge.Width, (avgMs / GaugeScaleMs) * gauge.Width );
			if ( _smoothedAvgWidth.TryGetValue( name, out var prev ) )
				targetAvgWidth = MathX.LerpTo( prev, targetAvgWidth, Time.Delta * 14 );
			_smoothedAvgWidth[name] = targetAvgWidth;
			Hud.DrawRect( new Rect( gauge.Left, gauge.Top, MathF.Max( 1, targetAvgWidth ), gauge.Height ), color.WithAlpha( 0.65f ) );

			var lastX = gauge.Left + MathF.Min( gauge.Width, (lastMs / GaugeScaleMs) * gauge.Width );
			Hud.DrawRect( new Rect( lastX, gauge.Top, 1, gauge.Height ), color.Lighten( 0.2f ) );

			var maxX = gauge.Left + MathF.Min( gauge.Width, (maxMs / GaugeScaleMs) * gauge.Width );
			Hud.DrawRect( new Rect( maxX, gauge.Top, 1, gauge.Height ), Color.White.WithAlpha( 0.25f ) );

			var valueColor = Color.White.WithAlpha( 0.85f );
			DrawTextCell( $"{lastMs:F2}ms", valueColor, colLast, y, ValueWidth, TextFlag.LeftCenter );
			DrawTextCell( $"{avgMs:F2}ms", valueColor, colAvg, y, ValueWidth, TextFlag.LeftCenter );
			DrawTextCell( $"{maxMs:F2}ms", valueColor, colMax, y, ValueWidth, TextFlag.LeftCenter );

			y += RowHeight + 1;
		}

		static void DrawTextCell( string text, Color color, float x, float y, float width, TextFlag flag )
		{
			var scope = new TextRendering.Scope( text, color, 11, "Roboto Mono", 600 ) { Outline = _outline };
			Hud.DrawText( scope, new Rect( x, y, width, RowHeight ), flag );
		}
	}
}
