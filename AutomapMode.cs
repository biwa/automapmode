
#region ================== Copyright (c) 2007 Pascal vd Heiden

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using CodeImp.DoomBuilder.Windows;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Actions;
using CodeImp.DoomBuilder.Types;
using CodeImp.DoomBuilder.Config;

#endregion

namespace CodeImp.DoomBuilder.AutomapMode
{
	[EditMode(DisplayName = "Automap Mode",
			  SwitchAction = "automapmode",	// Action name used to switch to this mode
			  ButtonImage = "automap.png",	// Image resource name for the button
			  ButtonOrder = int.MinValue + 100,	// Position of the button (lower is more to the left)
			  ButtonGroup = "000_editing",
			  UseByDefault = true,
			  SafeStartMode = true)]

	public class AutomapMode : ClassicMode
	{
		#region ================== Constants

		#endregion

		#region ================== Variables

		private CustomPresentation automappresentation;
		private List<Linedef> validlinedefs;

		// Highlighted item
		private Linedef highlighted;
		
		// Interface
		private bool editpressed;
		
		#endregion

		#region ================== Properties

		public override object HighlightedObject { get { return highlighted; } }
		
		#endregion

		#region ================== Constructor / Disposer

		#endregion

		#region ================== Methods

		// This highlights a new item
		protected void Highlight(Linedef l)
		{
			bool completeredraw = false;
			LinedefActionInfo action = null;

			// Often we can get away by simply undrawing the previous
			// highlight and drawing the new highlight. But if associations
			// are or were drawn we need to redraw the entire display.
			
			// Previous association highlights something?
			if((highlighted != null) && (highlighted.Tag > 0)) completeredraw = true;
			
			// New association highlights something?
			if((l != null) && (l.Tag > 0)) completeredraw = true;

			General.Interface.RedrawDisplay();

			// If we're changing associations, then we
			// need to redraw the entire display
			if(completeredraw)
			{
				// Set new highlight and redraw completely
				highlighted = l;
				General.Interface.RedrawDisplay();
			}
			else
			{
				// Update display
				if(renderer.StartPlotter(false))
				{
					// Undraw previous highlight
					if((highlighted != null) && !highlighted.IsDisposed)
					{
						// renderer.PlotLine(highlighted.Start.Position, highlighted.End.Position, new PixelColor(255, 0, 0, 0));
						//renderer.PlotVertex(highlighted.Start, renderer.DetermineVertexColor(highlighted.Start));
						//renderer.PlotVertex(highlighted.End, renderer.DetermineVertexColor(highlighted.End));
					}

					// Set new highlight
					highlighted = l;

					// Render highlighted item
					if((highlighted != null) && !highlighted.IsDisposed && LinedefIsValid(highlighted))
					{
						// renderer.PlotLine(highlighted.Start.Position, highlighted.End.Position, General.Colors.Highlight);
						//renderer.PlotVertex(highlighted.Start, renderer.DetermineVertexColor(highlighted.Start));
						//renderer.PlotVertex(highlighted.End, renderer.DetermineVertexColor(highlighted.End));
					}

					// Done
					renderer.Finish();
					renderer.Present();
				}
			}

			General.Interface.RedrawDisplay();

			// Show highlight info
			if((highlighted != null) && !highlighted.IsDisposed)
				General.Interface.ShowLinedefInfo(highlighted);
			else
				General.Interface.HideInfo();
		}

		private List<Linedef> GetValidLinedefs()
		{
			List<Linedef> linedefs = new List<Linedef>();

			foreach (Linedef ld in General.Map.Map.Linedefs)
				if (LinedefIsValid(ld)) linedefs.Add(ld);

			return linedefs;
		}

		private PixelColor DetermineLinedefColor(Linedef ld)
		{
			if (ld.IsFlagSet(BuilderPlug.Me.HiddenFlag))
				return new PixelColor(255, 192, 192, 192);
			if (ld.Back == null || ld.IsFlagSet(BuilderPlug.Me.SecretFlag))
				return new PixelColor(255, 252, 0, 0);
			else if (ld.Front.Sector.FloorHeight != ld.Back.Sector.FloorHeight)
				return new PixelColor(255, 188, 120, 72);
			else if (ld.Front.Sector.CeilHeight != ld.Back.Sector.CeilHeight)
				return new PixelColor(255, 252, 252, 0);
			else if (ld.Front.Sector.CeilHeight == ld.Back.Sector.CeilHeight && ld.Front.Sector.FloorHeight == ld.Back.Sector.FloorHeight)
				return new PixelColor(255, 128, 128, 128);
			else if (General.Interface.CtrlState)
				new PixelColor(255, 192, 192, 192);

			return new PixelColor(255, 255, 255, 255);
		}

		private bool LinedefIsValid(Linedef ld)
		{
			if (General.Interface.CtrlState) return true;
			if (ld.IsFlagSet(BuilderPlug.Me.HiddenFlag)) return false;
			if (ld.Back == null || ld.IsFlagSet(BuilderPlug.Me.SecretFlag)) return true;
			if (ld.Back != null && (ld.Front.Sector.FloorHeight != ld.Back.Sector.FloorHeight || ld.Front.Sector.CeilHeight != ld.Back.Sector.CeilHeight)) return true;

			return false;
		}

		#endregion
		
		#region ================== Events

		public override void OnHelp()
		{
			General.ShowHelp("e_linedefs.html");
		}

		// Cancel mode
		public override void OnCancel()
		{
			base.OnCancel();

			// Return to this mode
			General.Editing.ChangeMode(new AutomapMode());
		}

		// Mode engages
		public override void OnEngage()
		{
			base.OnEngage();
			renderer.SetPresentation(Presentation.Standard);

			// Automap presentation without the surfaces
			automappresentation = new CustomPresentation();
			automappresentation.AddLayer(new PresentLayer(RendererLayer.Background, BlendingMode.Mask, General.Settings.BackgroundAlpha));
			// automappresentation.AddLayer(new PresentLayer(RendererLayer.Surface, BlendingMode.Mask));
			automappresentation.AddLayer(new PresentLayer(RendererLayer.Things, BlendingMode.Alpha, General.Settings.InactiveThingsAlpha));
			automappresentation.AddLayer(new PresentLayer(RendererLayer.Grid, BlendingMode.Mask));
			automappresentation.AddLayer(new PresentLayer(RendererLayer.Geometry, BlendingMode.Alpha, 1f, true));
			automappresentation.AddLayer(new PresentLayer(RendererLayer.Overlay, BlendingMode.Alpha, 1f, true));

			renderer.SetPresentation(automappresentation);
			
			// Add toolbar buttons
			General.Interface.AddButton(BuilderPlug.Me.MenusForm.CopyProperties);
			General.Interface.AddButton(BuilderPlug.Me.MenusForm.PasteProperties);
			General.Interface.AddButton(BuilderPlug.Me.MenusForm.SeparatorCopyPaste);
			General.Interface.AddButton(BuilderPlug.Me.MenusForm.CurveLinedefs);
			
			// Convert geometry selection to linedefs selection
			General.Map.Map.ConvertSelection(SelectionType.Linedefs);

			validlinedefs = GetValidLinedefs();
		}
		
		// Mode disengages
		public override void OnDisengage()
		{
			base.OnDisengage();

			// Remove toolbar buttons
			General.Interface.RemoveButton(BuilderPlug.Me.MenusForm.CopyProperties);
			General.Interface.RemoveButton(BuilderPlug.Me.MenusForm.PasteProperties);
			General.Interface.RemoveButton(BuilderPlug.Me.MenusForm.SeparatorCopyPaste);
			General.Interface.RemoveButton(BuilderPlug.Me.MenusForm.CurveLinedefs);

			// Hide highlight info
			General.Interface.HideInfo();
		}

		// This redraws the display
		public override void OnRedrawDisplay()
		{
			renderer.RedrawSurface();
			// Render lines
			if(renderer.StartPlotter(true))
			{
				foreach (Linedef ld in General.Map.Map.Linedefs)
				{
					if (LinedefIsValid(ld))
						renderer.PlotLine(ld.Start.Position, ld.End.Position, DetermineLinedefColor(ld));
				}

				if ((highlighted != null) && !highlighted.IsDisposed && LinedefIsValid(highlighted))
				{
					renderer.PlotLine(highlighted.Start.Position, highlighted.End.Position, General.Colors.Highlight);
				}
				// renderer.PlotVerticesSet(General.Map.Map.Vertices);
				renderer.Finish();
			}

			// Render things
			if(renderer.StartThings(true))
			{
				// renderer.RenderThingSet(General.Map.ThingsFilter.HiddenThings, Presentation.THINGS_HIDDEN_ALPHA);
				// renderer.RenderThingSet(General.Map.ThingsFilter.VisibleThings, 1.0f);
				renderer.Finish();
			}

			// Render selection
			if(renderer.StartOverlay(true))
			{
				renderer.Finish();
			}

			renderer.Present();
		}

		protected override void OnSelectEnd()
		{
			// Item highlighted?
			if((highlighted != null) && !highlighted.IsDisposed)
			{
				General.Map.UndoRedo.CreateUndo("Toggle linedef show as 1-sided on automap flag");

				// Toggle flag
				highlighted.SetFlag(BuilderPlug.Me.SecretFlag, !highlighted.IsFlagSet(BuilderPlug.Me.SecretFlag));
				validlinedefs = GetValidLinedefs();
			}

			base.OnSelectEnd();
		}
		
		protected override void OnEditEnd()
		{
			// Item highlighted?
			if ((highlighted != null) && !highlighted.IsDisposed)
			{
				General.Map.UndoRedo.CreateUndo("Toggle linedef not shown on automap flag");

				// Toggle flag
				highlighted.SetFlag(BuilderPlug.Me.HiddenFlag, !highlighted.IsFlagSet(BuilderPlug.Me.HiddenFlag));
				validlinedefs = GetValidLinedefs();
				General.Interface.RedrawDisplay();
			}

			base.OnEditEnd();
		}
		
		// Mouse moves
		public override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);

			// Not holding any buttons?
			if(e.Button == MouseButtons.None)
			{
				// Find the nearest linedef within highlight range
				Linedef l = MapSet.NearestLinedefRange(validlinedefs, mousemappos, BuilderPlug.Me.HighlightRange / renderer.Scale);

				// Highlight if not the same
				if(l != highlighted) Highlight(l);
			}
		}

		// Mouse leaves
		public override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);

			// Highlight nothing
			Highlight(null);
		}

		public override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);

			if (e.Control)
			{
				validlinedefs = GetValidLinedefs();
				General.Interface.RedrawDisplay();
			}
		}

		public override void OnKeyUp(KeyEventArgs e)
		{
			base.OnKeyUp(e);

			if (!e.Control)
			{
				validlinedefs = GetValidLinedefs();
				General.Interface.RedrawDisplay();
			}
		}

		#endregion

		#region ================== Actions

		// Do something with selected line(s)
		[BeginAction("insertitem", BaseAction = true)]
		public virtual void InsertVertexAction()
		{
		}

		// Do something with selected line(s)
		[BeginAction("deleteitem", BaseAction = true)]
		public void DeleteItem()
		{
		}

		#endregion
	}
}
