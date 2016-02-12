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
        public string Author
        {
            get
            {
                return ((AssemblyCopyrightAttribute)base.GetType().Assembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false)[0]).Copyright;
            }
        }
        public string Copyright
        {
            get
            {
                return ((AssemblyDescriptionAttribute)base.GetType().Assembly.GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false)[0]).Description;
            }
        }

        public string DisplayName
        {
            get
            {
                return ((AssemblyProductAttribute)base.GetType().Assembly.GetCustomAttributes(typeof(AssemblyProductAttribute), false)[0]).Product;
            }
        }

        public Version Version
        {
            get
            {
                return base.GetType().Assembly.GetName().Version;
            }
        }

        public Uri WebsiteUri
        {
            get
            {
                return new Uri("http://www.getpaint.net/redirect/plugins.html");
            }
        }
    }

    [PluginSupportInfo(typeof(PluginSupportInfo), DisplayName = "Inset Box Shadow")]
    public class InsetBoxShadowEffectPlugin : PropertyBasedEffect
    {
        public static string StaticName
        {
            get
            {
                return "Inset Box Shadow";
            }
        }

        public static Image StaticIcon
        {
            get
            {
                return new Bitmap(typeof(InsetBoxShadowEffectPlugin), "InsetBoxShadow.png");
            }
        }

        public static string SubmenuName
        {
            get
            {
                return SubmenuNames.Photo;
            }
        }

        public InsetBoxShadowEffectPlugin()
            : base(StaticName, StaticIcon, SubmenuName, EffectFlags.Configurable)
        {
        }

        public enum PropertyNames
        {
            Amount1,
            Amount2,
            Amount3,
            Amount4,
            Amount5
        }


        protected override PropertyCollection OnCreatePropertyCollection()
        {
            Rectangle selection = EnvironmentParameters.GetSelection(EnvironmentParameters.SourceSurface.Bounds).GetBoundsInt();
            int marginMax = (int)Math.Min(selection.Height / 2.5, selection.Width / 2.5);

            List<Property> props = new List<Property>();

            props.Add(new Int32Property(PropertyNames.Amount1, 15, 0, marginMax));
            props.Add(new Int32Property(PropertyNames.Amount2, 1, 1, 100));
            props.Add(new Int32Property(PropertyNames.Amount3, 20, 0, 100));
            props.Add(new Int32Property(PropertyNames.Amount5, 255, 0, 255));
            props.Add(new Int32Property(PropertyNames.Amount4, ColorBgra.ToOpaqueInt32(ColorBgra.FromBgra(EnvironmentParameters.PrimaryColor.B, EnvironmentParameters.PrimaryColor.G, EnvironmentParameters.PrimaryColor.R, 255)), 0, 0xffffff));

            return new PropertyCollection(props);
        }

        protected override ControlInfo OnCreateConfigUI(PropertyCollection props)
        {
            ControlInfo configUI = CreateDefaultConfigUI(props);

            configUI.SetPropertyControlValue(PropertyNames.Amount1, ControlInfoPropertyNames.DisplayName, "Margin");
            configUI.SetPropertyControlValue(PropertyNames.Amount2, ControlInfoPropertyNames.DisplayName, "Spread");
            configUI.SetPropertyControlValue(PropertyNames.Amount3, ControlInfoPropertyNames.DisplayName, "Blur");
            configUI.SetPropertyControlValue(PropertyNames.Amount4, ControlInfoPropertyNames.DisplayName, "Color");
            configUI.SetPropertyControlType(PropertyNames.Amount4, PropertyControlType.ColorWheel);
            configUI.SetPropertyControlValue(PropertyNames.Amount5, ControlInfoPropertyNames.DisplayName, "Opacity");
            configUI.SetPropertyControlValue(PropertyNames.Amount5, ControlInfoPropertyNames.ControlColors, new ColorBgra[] { ColorBgra.White, ColorBgra.Black });

            return configUI;
        }

        protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken newToken, RenderArgs dstArgs, RenderArgs srcArgs)
        {
            Amount1 = newToken.GetProperty<Int32Property>(PropertyNames.Amount1).Value;
            Amount2 = newToken.GetProperty<Int32Property>(PropertyNames.Amount2).Value;
            Amount3 = newToken.GetProperty<Int32Property>(PropertyNames.Amount3).Value;
            Amount4 = ColorBgra.FromOpaqueInt32(newToken.GetProperty<Int32Property>(PropertyNames.Amount4).Value);
            Amount5 = newToken.GetProperty<Int32Property>(PropertyNames.Amount5).Value;


            if (shadowSurface == null)
                shadowSurface = new Surface(srcArgs.Surface.Size);
            else
                shadowSurface.Clear(Color.Transparent);

            Rectangle selection = EnvironmentParameters.GetSelection(srcArgs.Surface.Bounds).GetBoundsInt();

            PointF topStart = new PointF(selection.Left, selection.Top + (Amount1 + Amount2) / 2f);
            PointF topEnd = new PointF(selection.Right, selection.Top + (Amount1 + Amount2) / 2f);
            PointF rightStart = new PointF(selection.Right - (Amount1 + Amount2) / 2f, selection.Top);
            PointF rightEnd = new PointF(selection.Right - (Amount1 + Amount2) / 2f, selection.Bottom);
            PointF bottomStart = new PointF(selection.Left, selection.Bottom - (Amount1 + Amount2) / 2f);
            PointF bottomEnd = new PointF(selection.Right, selection.Bottom - (Amount1 + Amount2) / 2f);
            PointF leftStart = new PointF(selection.Left + (Amount1 + Amount2) / 2f, selection.Top);
            PointF leftEnd = new PointF(selection.Left + (Amount1 + Amount2) / 2f, selection.Bottom);

            using (RenderArgs ra = new RenderArgs(shadowSurface))
            {
                Graphics shadow = ra.Graphics;
                using (Pen shadowPen = new Pen(Amount4, Amount1 + Amount2))
                {
                    // Because DrawRectangle Sucks! 
                    shadow.DrawLine(shadowPen, topStart, topEnd);
                    shadow.DrawLine(shadowPen, rightStart, rightEnd);
                    shadow.DrawLine(shadowPen, bottomStart, bottomEnd);
                    shadow.DrawLine(shadowPen, leftStart, leftEnd);
                }
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


        int Amount1 = 20; // [0,100] Margin
        int Amount2 = 20; // [0,100] Spread
        int Amount3 = 20; // [0,100] Blur
        ColorBgra Amount4 = ColorBgra.FromBgr(0, 0, 0); // Color
        int Amount5 = 255; // [0,255] Opacity

        BinaryPixelOp normalOp = LayerBlendModeUtil.CreateCompositionOp(LayerBlendMode.Normal);
        Surface shadowSurface;

        void Render(Surface dst, Surface src, Rectangle rect)
        {
            if (Amount3 != 0)
            {
                // Setup for calling the Gaussian Blur effect
                GaussianBlurEffect blurEffect = new GaussianBlurEffect();
                PropertyCollection blurProps = blurEffect.CreatePropertyCollection();
                PropertyBasedEffectConfigToken BlurParameters = new PropertyBasedEffectConfigToken(blurProps);
                BlurParameters.SetPropertyValue(GaussianBlurEffect.PropertyNames.Radius, Amount3);
                blurEffect.SetRenderInfo(BlurParameters, new RenderArgs(dst), new RenderArgs(shadowSurface));
                // Call the Gaussian Blur function
                blurEffect.Render(new Rectangle[1] { rect }, 0, 1);
            }
            else
            {
                dst.CopySurface(shadowSurface, rect.Location, rect);
            }

            Rectangle selection = EnvironmentParameters.GetSelection(src.Bounds).GetBoundsInt();
            ColorBgra sourcePixel, shadowPixel;

            for (int y = rect.Top; y < rect.Bottom; y++)
            {
                if (IsCancelRequested) return;
                for (int x = rect.Left; x < rect.Right; x++)
                {
                    sourcePixel = src[x, y];
                    shadowPixel = dst[x, y];

                    if (x < selection.Left + Amount1 || x > selection.Right - Amount1 - 1 || y < selection.Top + Amount1 || y > selection.Bottom - Amount1 - 1)
                    {
                        // Erase the margins
                        shadowPixel.A = 0;
                    }
                    else
                    {
                        shadowPixel.A = Int32Util.ClampToByte(shadowPixel.A * Amount5 / 255);
                    }

                    dst[x, y] = normalOp.Apply(sourcePixel, shadowPixel);
                }
            }
        }
    }
}
