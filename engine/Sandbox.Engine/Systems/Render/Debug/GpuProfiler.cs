using Sandbox.Diagnostics;

namespace Sandbox;

internal static partial class DebugOverlay
{
	public partial class GpuProfiler
	{
		private static readonly Color[] PassColors = new[]
		{
			new Color( 0.4f, 0.7f, 1.0f ),   // Blue
			new Color( 0.4f, 1.0f, 0.5f ),   // Green
			new Color( 1.0f, 0.7f, 0.3f ),   // Orange
			new Color( 1.0f, 0.4f, 0.4f ),   // Red
			new Color( 0.8f, 0.5f, 1.0f ),   // Purple
			new Color( 1.0f, 1.0f, 0.4f ),   // Yellow
			new Color( 0.5f, 1.0f, 1.0f ),   // Cyan
			new Color( 1.0f, 0.5f, 0.8f ),   // Pink
		};

		private static readonly TextRendering.Outline Outline = new() { Color = Color.Black.WithAlpha( 0.8f ), Size = 2, Enabled = true };

		private const float RowHeight = 14f;
		private const float NameWidth = 170f;
		private const float GaugeWidth = 120f;
		private const float ValueWidth = 74f;
		private const int MaxRows = 20;
		private const float MinVisibleMs = 0.02f;

		internal static void Draw( ref Vector2 pos )
		{
			var entries = GpuProfilerStats.Entries;
			if ( entries.Count == 0 )
			{
				DrawNoData( ref pos );
				return;
			}

			var rows = entries
				.Select( (entry, index) => (
					Name: entry.Name,
					LastMs: entry.DurationMs,
					SmoothMs: GpuProfilerStats.GetSmoothedDuration( entry.Name ),
					Color: PassColors[index % PassColors.Length]
				) )
				.Where( x => x.SmoothMs >= MinVisibleMs && !x.Name.StartsWith( "Managed:" ) )
				.OrderByDescending( x => x.SmoothMs )
				.Take( MaxRows )
				.ToList();

			if ( rows.Count == 0 )
			{
				DrawNoData( ref pos );
				return;
			}

			var totalMs = MathF.Max( GpuProfilerStats.TotalGpuTimeMs, 0.001f );
			var scaleMs = MathF.Max( totalMs, rows.Max( x => x.SmoothMs ) );

			var x = pos.x;
			var y = pos.y;
			var colName = x;
			var colGauge = colName + NameWidth + 8;
			var colLast = colGauge + GaugeWidth + 10;
			var colSmooth = colLast + ValueWidth;
			var colShare = colSmooth + ValueWidth;

			DrawSummary( ref y, x, totalMs, rows.Count );
			DrawHeader( ref y, colName, colLast, colSmooth, colShare );

			foreach ( var row in rows )
			{
				DrawRow( ref y, row, totalMs, scaleMs, colName, colGauge, colLast, colSmooth, colShare );
			}

			pos.y = y;
		}

		private static void DrawSummary( ref float y, float x, float totalMs, int shownRows )
		{
			var fpsMax = 1000f / totalMs;
			var color = totalMs > 16.67f ? new Color( 1f, 0.65f, 0.35f ) : Color.White.WithAlpha( 0.9f );
			var scope = new TextRendering.Scope( $"GPU total {totalMs:F2}ms  ({fpsMax:F0} fps max)  shown {shownRows}", color, 11, "Roboto Mono", 700 ) { Outline = Outline };
			Hud.DrawText( scope, new Rect( x, y, 560, RowHeight ), TextFlag.LeftCenter );
			y += RowHeight;
		}

		private static void DrawHeader( ref float y, float colName, float colLast, float colSmooth, float colShare )
		{
			var dim = Color.White.WithAlpha( 0.55f );
			DrawCell( "pass", dim, colName, y, NameWidth, TextFlag.LeftCenter );
			DrawCell( "last", dim, colLast, y, ValueWidth, TextFlag.LeftCenter );
			DrawCell( "avg", dim, colSmooth, y, ValueWidth, TextFlag.LeftCenter );
			DrawCell( "% total", dim, colShare, y, ValueWidth, TextFlag.LeftCenter );
			y += RowHeight;
		}

		private static void DrawRow( ref float y, (string Name, float LastMs, float SmoothMs, Color Color) row, float totalMs, float scaleMs, float colName, float colGauge, float colLast, float colSmooth, float colShare )
		{
			DrawCell( row.Name, row.Color.Lighten( 0.45f ), colName, y, NameWidth, TextFlag.LeftCenter );

			var gauge = new Rect( colGauge, y + 2, GaugeWidth, RowHeight - 4 );
			Hud.DrawRect( gauge, Color.Black.WithAlpha( 0.2f ), borderWidth: 1, borderColor: Color.White.WithAlpha( 0.08f ) );

			var smoothW = MathF.Min( gauge.Width, (row.SmoothMs / scaleMs) * gauge.Width );
			Hud.DrawRect( new Rect( gauge.Left, gauge.Top, MathF.Max( 1, smoothW ), gauge.Height ), row.Color.WithAlpha( 0.65f ) );

			var lastX = gauge.Left + MathF.Min( gauge.Width, (row.LastMs / scaleMs) * gauge.Width );
			Hud.DrawRect( new Rect( lastX, gauge.Top, 1, gauge.Height ), row.Color.Lighten( 0.2f ) );

			var sharePct = (row.LastMs / totalMs) * 100f;
			DrawCell( $"{row.LastMs:F2}ms", Color.White.WithAlpha( 0.85f ), colLast, y, ValueWidth, TextFlag.LeftCenter );
			DrawCell( $"{row.SmoothMs:F2}ms", Color.White.WithAlpha( 0.7f ), colSmooth, y, ValueWidth, TextFlag.LeftCenter );
			DrawCell( $"{sharePct:F1}%", Color.White.WithAlpha( 0.7f ), colShare, y, ValueWidth, TextFlag.LeftCenter );

			y += RowHeight + 1;
		}

		private static void DrawCell( string text, Color color, float x, float y, float width, TextFlag flag )
		{
			var scope = new TextRendering.Scope( text, color, 11, "Roboto Mono", 600 ) { Outline = Outline };
			Hud.DrawText( scope, new Rect( x, y, width, RowHeight ), flag );
		}

		private static void DrawNoData( ref Vector2 pos )
		{
			var scope = new TextRendering.Scope( "GPU profiler: waiting for data...", Color.White.WithAlpha( 0.6f ), 11, "Roboto Mono", 600 ) { Outline = Outline };
			Hud.DrawText( scope, new Rect( pos, new Vector2( 320, RowHeight ) ), TextFlag.LeftCenter );
			pos.y += RowHeight;
		}

	}
}
