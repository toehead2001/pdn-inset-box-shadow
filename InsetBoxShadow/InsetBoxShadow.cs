using System;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using PaintDotNet;
using PaintDotNet.Effects;
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;

namespace InsetBoxShadowEffect
{
    public class PluginSupportInfo : IPluginSupportInfo
    {
        public string Author => base.GetType().Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright;
        public string Copyright => base.GetType().Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
        public string DisplayName => base.GetType().Assembly.GetCustomAttribute<AssemblyProductAttribute>().Product;
        public Version Version => base.GetType().Assembly.GetName().Version;
        public Uri WebsiteUri => new Uri("https://forums.getpaint.net/index.php?showtopic=107381");
    }

    [PluginSupportInfo(typeof(PluginSupportInfo), DisplayName = "Inset Box Shadow")]
    public class InsetBoxShadowEffectPlugin : PropertyBasedEffect
    {
        private int margin = 20;
        private int spread = 20;
        private int blur = 20;
        private ColorBgra color = ColorBgra.Black;
        private int offsetX = 0;
        private int offsetY = 0;

        private readonly BinaryPixelOp normalOp = LayerBlendModeUtil.CreateCompositionOp(LayerBlendMode.Normal);
        private readonly GaussianBlurEffect blurEffect = new GaussianBlurEffect();
        private Surface shadowSurface;

        private static readonly Image StaticIcon = new Bitmap(typeof(InsetBoxShadowEffectPlugin), "InsetBoxShadow.png");

        public InsetBoxShadowEffectPlugin()
            : base("Inset Box Shadow", StaticIcon, SubmenuNames.Render, new EffectOptions() { Flags = EffectFlags.Configurable })
        {
        }

        private enum PropertyNames
        {
            Margin,
            Spread,
            Blur,
            Color,
            OffsetX,
            OffsetY
        }

        protected override PropertyCollection OnCreatePropertyCollection()
        {
            Rectangle selection = EnvironmentParameters.GetSelection(EnvironmentParameters.SourceSurface.Bounds).GetBoundsInt();
            int marginMax = (int)Math.Min(selection.Height / 2.5, selection.Width / 2.5);
            int offsetXMax = selection.Width;
            int offsetYMax = selection.Height;

            List<Property> props = new List<Property>
            {
                new Int32Property(PropertyNames.Margin, 15, 0, marginMax),
                new Int32Property(PropertyNames.OffsetX, 0, -offsetXMax, offsetXMax),
                new Int32Property(PropertyNames.OffsetY, 0, -offsetYMax, offsetYMax),
                new Int32Property(PropertyNames.Spread, 1, 1, 100),
                new Int32Property(PropertyNames.Blur, 20, 0, 100),
                new Int32Property(PropertyNames.Color, unchecked((int)EnvironmentParameters.PrimaryColor.Bgra), int.MinValue, int.MaxValue)
            };

            return new PropertyCollection(props);
        }

        protected override ControlInfo OnCreateConfigUI(PropertyCollection props)
        {
            ControlInfo configUI = CreateDefaultConfigUI(props);

            configUI.SetPropertyControlValue(PropertyNames.Margin, ControlInfoPropertyNames.DisplayName, "Baseline Margin");
            configUI.SetPropertyControlValue(PropertyNames.OffsetX, ControlInfoPropertyNames.DisplayName, "Offset X");
            configUI.SetPropertyControlValue(PropertyNames.OffsetY, ControlInfoPropertyNames.DisplayName, "Offset Y");
            configUI.SetPropertyControlValue(PropertyNames.Spread, ControlInfoPropertyNames.DisplayName, "Spread");
            configUI.SetPropertyControlValue(PropertyNames.Blur, ControlInfoPropertyNames.DisplayName, "Blur");
            configUI.SetPropertyControlValue(PropertyNames.Color, ControlInfoPropertyNames.DisplayName, "Color");
            configUI.SetPropertyControlType(PropertyNames.Color, PropertyControlType.ColorWheel);

            return configUI;
        }

        protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken newToken, RenderArgs dstArgs, RenderArgs srcArgs)
        {
            margin = newToken.GetProperty<Int32Property>(PropertyNames.Margin).Value;
            spread = newToken.GetProperty<Int32Property>(PropertyNames.Spread).Value;
            blur = newToken.GetProperty<Int32Property>(PropertyNames.Blur).Value;
            color = ColorBgra.FromUInt32(unchecked((uint)newToken.GetProperty<Int32Property>(PropertyNames.Color).Value));
            offsetX = newToken.GetProperty<Int32Property>(PropertyNames.OffsetX).Value;
            offsetY = newToken.GetProperty<Int32Property>(PropertyNames.OffsetY).Value;

            if (shadowSurface == null)
                shadowSurface = new Surface(srcArgs.Surface.Size);
            else
                shadowSurface.Clear(Color.Transparent);

            // Setup for calling the Gaussian Blur effect
            PropertyCollection blurProps = blurEffect.CreatePropertyCollection();
            PropertyBasedEffectConfigToken BlurParameters = new PropertyBasedEffectConfigToken(blurProps);
            BlurParameters.SetPropertyValue(GaussianBlurEffect.PropertyNames.Radius, blur);
            blurEffect.SetRenderInfo(BlurParameters, dstArgs, new RenderArgs(shadowSurface));

            Rectangle selection = EnvironmentParameters.GetSelection(srcArgs.Surface.Bounds).GetBoundsInt();

            PointF topStart = new PointF(selection.Left, selection.Top + (margin + spread + offsetY) / 2f);
            PointF topEnd = new PointF(selection.Right, selection.Top + (margin + spread + offsetY) / 2f);
            PointF rightStart = new PointF(selection.Right - (margin + spread - offsetX) / 2f, selection.Top);
            PointF rightEnd = new PointF(selection.Right - (margin + spread - offsetX) / 2f, selection.Bottom);
            PointF bottomStart = new PointF(selection.Left, selection.Bottom - (margin + spread - offsetY) / 2f);
            PointF bottomEnd = new PointF(selection.Right, selection.Bottom - (margin + spread - offsetY) / 2f);
            PointF leftStart = new PointF(selection.Left + (margin + spread + offsetX) / 2f, selection.Top);
            PointF leftEnd = new PointF(selection.Left + (margin + spread + offsetX) / 2f, selection.Bottom);

            using (Graphics shadow = new RenderArgs(shadowSurface).Graphics)
            using (Pen shadowPen = new Pen(color))
            {
                shadowPen.Width = margin + spread + offsetX;
                shadow.DrawLine(shadowPen, leftStart, leftEnd);

                shadowPen.Width = margin + spread - offsetX;
                shadow.DrawLine(shadowPen, rightStart, rightEnd);

                shadowPen.Width = margin + spread + offsetY;
                shadow.DrawLine(shadowPen, topStart, topEnd);

                shadowPen.Width = margin + spread - offsetY;
                shadow.DrawLine(shadowPen, bottomStart, bottomEnd);
            }

            base.OnSetRenderInfo(newToken, dstArgs, srcArgs);
        }

        protected override void OnRender(Rectangle[] renderRects, int startIndex, int length)
        {
            if (length == 0) return;
            for (int i = startIndex; i < startIndex + length; ++i)
            {
                Render(DstArgs.Surface, SrcArgs.Surface, renderRects[i]);
            }
        }

        private void Render(Surface dst, Surface src, Rectangle rect)
        {
            if (blur != 0)
            {
                // Call the Gaussian Blur function
                blurEffect.Render(new Rectangle[] { rect }, 0, 1);
            }
            else
            {
                dst.CopySurface(shadowSurface, rect.Location, rect);
            }

            Rectangle selection = EnvironmentParameters.GetSelection(src.Bounds).GetBoundsInt();
            Rectangle shadowRect = Rectangle.FromLTRB(
                selection.Left + margin + offsetX,
                selection.Top + margin + offsetY,
                selection.Right - margin + offsetX,
                selection.Bottom - margin + offsetY);
            ColorBgra shadowPixel;

            for (int y = rect.Top; y < rect.Bottom; y++)
            {
                if (IsCancelRequested) return;
                for (int x = rect.Left; x < rect.Right; x++)
                {
                    shadowPixel = dst[x, y];
                    shadowPixel.A = shadowRect.Contains(x, y) ? Int32Util.ClampToByte(shadowPixel.A * color.A / byte.MaxValue) : byte.MinValue;

                    dst[x, y] = normalOp.Apply(src[x, y], shadowPixel);
                }
            }
        }
    }
}
