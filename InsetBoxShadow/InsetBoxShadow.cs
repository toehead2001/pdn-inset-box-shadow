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
        private int Amount1 = 20; // [0,100] Margin
        private int Amount2 = 20; // [0,100] Spread
        private int Amount3 = 20; // [0,100] Blur
        private ColorBgra Amount4 = ColorBgra.Black; // Color
        private int Amount6 = 0; // [-50, 50] Offset X
        private int Amount7 = 0; // [-50, 50] Offset Y

        private readonly BinaryPixelOp normalOp = LayerBlendModeUtil.CreateCompositionOp(LayerBlendMode.Normal);
        private readonly GaussianBlurEffect blurEffect = new GaussianBlurEffect();
        private Surface shadowSurface;

        private const string StaticName = "Inset Box Shadow";
        private static readonly Image StaticIcon = new Bitmap(typeof(InsetBoxShadowEffectPlugin), "InsetBoxShadow.png");

        public InsetBoxShadowEffectPlugin()
            : base(StaticName, StaticIcon, SubmenuNames.Photo, EffectFlags.Configurable)
        {
        }

        public enum PropertyNames
        {
            Amount1,
            Amount2,
            Amount3,
            Amount4,
            Amount6,
            Amount7
        }

        protected override PropertyCollection OnCreatePropertyCollection()
        {
            Rectangle selection = EnvironmentParameters.GetSelection(EnvironmentParameters.SourceSurface.Bounds).GetBoundsInt();
            int marginMax = (int)Math.Min(selection.Height / 2.5, selection.Width / 2.5);
            int offsetXMax = selection.Width;
            int offsetYMax = selection.Height;

            List<Property> props = new List<Property>
            {
                new Int32Property(PropertyNames.Amount1, 15, 0, marginMax),
                new Int32Property(PropertyNames.Amount6, 0, -offsetXMax, offsetXMax),
                new Int32Property(PropertyNames.Amount7, 0, -offsetYMax, offsetYMax),
                new Int32Property(PropertyNames.Amount2, 1, 1, 100),
                new Int32Property(PropertyNames.Amount3, 20, 0, 100),
                new Int32Property(PropertyNames.Amount4, unchecked((int)EnvironmentParameters.PrimaryColor.Bgra), int.MinValue, int.MaxValue)
            };

            return new PropertyCollection(props);
        }

        protected override ControlInfo OnCreateConfigUI(PropertyCollection props)
        {
            ControlInfo configUI = CreateDefaultConfigUI(props);

            configUI.SetPropertyControlValue(PropertyNames.Amount1, ControlInfoPropertyNames.DisplayName, "Baseline Margin");
            configUI.SetPropertyControlValue(PropertyNames.Amount6, ControlInfoPropertyNames.DisplayName, "Offset X");
            configUI.SetPropertyControlValue(PropertyNames.Amount7, ControlInfoPropertyNames.DisplayName, "Offset Y");
            configUI.SetPropertyControlValue(PropertyNames.Amount2, ControlInfoPropertyNames.DisplayName, "Spread");
            configUI.SetPropertyControlValue(PropertyNames.Amount3, ControlInfoPropertyNames.DisplayName, "Blur");
            configUI.SetPropertyControlValue(PropertyNames.Amount4, ControlInfoPropertyNames.DisplayName, "Color");
            configUI.SetPropertyControlType(PropertyNames.Amount4, PropertyControlType.ColorWheel);

            return configUI;
        }

        protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken newToken, RenderArgs dstArgs, RenderArgs srcArgs)
        {
            Amount1 = newToken.GetProperty<Int32Property>(PropertyNames.Amount1).Value;
            Amount2 = newToken.GetProperty<Int32Property>(PropertyNames.Amount2).Value;
            Amount3 = newToken.GetProperty<Int32Property>(PropertyNames.Amount3).Value;
            Amount4 = ColorBgra.FromUInt32(unchecked((uint)newToken.GetProperty<Int32Property>(PropertyNames.Amount4).Value));
            Amount6 = newToken.GetProperty<Int32Property>(PropertyNames.Amount6).Value;
            Amount7 = newToken.GetProperty<Int32Property>(PropertyNames.Amount7).Value;

            if (shadowSurface == null)
                shadowSurface = new Surface(srcArgs.Surface.Size);
            else
                shadowSurface.Clear(Color.Transparent);

            // Setup for calling the Gaussian Blur effect
            PropertyCollection blurProps = blurEffect.CreatePropertyCollection();
            PropertyBasedEffectConfigToken BlurParameters = new PropertyBasedEffectConfigToken(blurProps);
            BlurParameters.SetPropertyValue(GaussianBlurEffect.PropertyNames.Radius, Amount3);
            blurEffect.SetRenderInfo(BlurParameters, dstArgs, new RenderArgs(shadowSurface));

            Rectangle selection = EnvironmentParameters.GetSelection(srcArgs.Surface.Bounds).GetBoundsInt();

            PointF topStart = new PointF(selection.Left, selection.Top + (Amount1 + Amount2 + Amount7) / 2f);
            PointF topEnd = new PointF(selection.Right, selection.Top + (Amount1 + Amount2 + Amount7) / 2f);
            PointF rightStart = new PointF(selection.Right - (Amount1 + Amount2 - Amount6) / 2f, selection.Top);
            PointF rightEnd = new PointF(selection.Right - (Amount1 + Amount2 - Amount6) / 2f, selection.Bottom);
            PointF bottomStart = new PointF(selection.Left, selection.Bottom - (Amount1 + Amount2 - Amount7) / 2f);
            PointF bottomEnd = new PointF(selection.Right, selection.Bottom - (Amount1 + Amount2 - Amount7) / 2f);
            PointF leftStart = new PointF(selection.Left + (Amount1 + Amount2 + Amount6) / 2f, selection.Top);
            PointF leftEnd = new PointF(selection.Left + (Amount1 + Amount2 + Amount6) / 2f, selection.Bottom);

            using (Graphics shadow = new RenderArgs(shadowSurface).Graphics)
            using (Pen shadowPen = new Pen(Amount4))
            {
                shadowPen.Width = Amount1 + Amount2 + Amount6;
                shadow.DrawLine(shadowPen, leftStart, leftEnd);

                shadowPen.Width = Amount1 + Amount2 - Amount6;
                shadow.DrawLine(shadowPen, rightStart, rightEnd);

                shadowPen.Width = Amount1 + Amount2 + Amount7;
                shadow.DrawLine(shadowPen, topStart, topEnd);

                shadowPen.Width = Amount1 + Amount2 - Amount7;
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
            if (Amount3 != 0)
            {
                // Call the Gaussian Blur function
                blurEffect.Render(new Rectangle[] { rect }, 0, 1);
            }
            else
            {
                dst.CopySurface(shadowSurface, rect.Location, rect);
            }

            Rectangle selection = EnvironmentParameters.GetSelection(src.Bounds).GetBoundsInt();
            ColorBgra shadowPixel;

            for (int y = rect.Top; y < rect.Bottom; y++)
            {
                if (IsCancelRequested) return;
                for (int x = rect.Left; x < rect.Right; x++)
                {
                    shadowPixel = dst[x, y];

                    if (x < selection.Left + Amount1 + Amount6 || x > selection.Right - Amount1 - 1 + Amount6 || y < selection.Top + Amount1 + Amount7 || y > selection.Bottom - Amount1 - 1 + Amount7)
                    {
                        // Erase the margins
                        shadowPixel.A = 0;
                    }
                    else
                    {
                        shadowPixel.A = Int32Util.ClampToByte(shadowPixel.A * Amount4.A / byte.MaxValue);
                    }

                    dst[x, y] = normalOp.Apply(src[x, y], shadowPixel);
                }
            }
        }
    }
}
