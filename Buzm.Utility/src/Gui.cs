using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Buzm.Utility
{
	public class Gui
	{
		public static void RoundEdges( Control control, int diameter )
		{
			// The main rectangle which will be rounded
			Size mainSize = new Size( control.Width, control.Height );
			Rectangle mainRect = new Rectangle( new Point(0, 0), mainSize );

			// The rectangle and graphics path that holds the rounded edge
			Rectangle arcRect = new Rectangle( mainRect.Location, new Size(diameter,diameter) );
			GraphicsPath path = new GraphicsPath();

			// top left arc
			path.AddArc(arcRect, 180, 90);

			// top right arc
			arcRect.X = mainRect.Right - diameter;
			path.AddArc(arcRect, 270, 90);

			// bottom right arc
			arcRect.Y = mainRect.Bottom - diameter;
			path.AddArc(arcRect, 0, 90);

			// bottom left arc
			arcRect.X = mainRect.Left;
			path.AddArc(arcRect, 90, 90);

			path.CloseFigure();
			control.Region = new Region( path );
		}
	}
}
