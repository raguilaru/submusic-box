using System;
using BoxSubMusic.App.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace BoxSubMusic.App.Controls
{
    /// <summary>
    /// Attaches an app-cached cover to an <see cref="Image"/> or an
    /// <see cref="ImageBrush"/> (§11, 2026-08-13).
    ///
    /// Replaces the `&lt;BitmapImage UriSource=... DecodePixelWidth=... /&gt;`
    /// the templates used to declare. The difference is invisible on screen and
    /// decisive for memory: a BitmapImage built from a URI is held in XAML's
    /// process-wide, URI-keyed cache, which the app cannot drop — dropping the
    /// whole visual tree reclaimed 0 MB on every console run of the background
    /// investigation for exactly that reason. <see cref="CoverArt"/> decodes
    /// from a stream instead, so the only reference is the app's own and
    /// artwork can be released on the way to the background.
    ///
    /// Usage, with the decode width first so it is set before the binding
    /// fires:
    /// <code>
    /// &lt;Image Stretch="UniformToFill"
    ///        controls:CachedCover.DecodeWidth="200"
    ///        controls:CachedCover.SourceUri="{x:Bind ArtworkUri}" /&gt;
    /// </code>
    ///
    /// Container recycling is handled with a per-element token: a row reused
    /// for a different album clears its image immediately and ignores the
    /// in-flight load it started for the previous one, so a cover can never
    /// land on the wrong row.
    /// </summary>
    public static class CachedCover
    {
        /// <summary>
        /// Decode size in effective pixels, which is also the display box —
        /// layout scaling is disabled, so 1 epx is 1 physical pixel and
        /// oversampling only costs memory (the standing rule from 2026-08-12).
        /// Declare this BEFORE <see cref="SourceUriProperty"/> in markup.
        /// </summary>
        public static readonly DependencyProperty DecodeWidthProperty =
            DependencyProperty.RegisterAttached(
                "DecodeWidth",
                typeof(int),
                typeof(CachedCover),
                new PropertyMetadata(0));

        public static int GetDecodeWidth(DependencyObject target)
        {
            return (int)target.GetValue(DecodeWidthProperty);
        }

        public static void SetDecodeWidth(DependencyObject target, int value)
        {
            target.SetValue(DecodeWidthProperty, value);
        }

        /// <summary>The cover to show; null clears the target.</summary>
        public static readonly DependencyProperty SourceUriProperty =
            DependencyProperty.RegisterAttached(
                "SourceUri",
                typeof(Uri),
                typeof(CachedCover),
                new PropertyMetadata(null, OnSourceUriChanged));

        public static Uri GetSourceUri(DependencyObject target)
        {
            return (Uri)target.GetValue(SourceUriProperty);
        }

        public static void SetSourceUri(DependencyObject target, Uri value)
        {
            target.SetValue(SourceUriProperty, value);
        }

        // Invalidates a load whose target has since been pointed elsewhere.
        private static readonly DependencyProperty LoadTokenProperty =
            DependencyProperty.RegisterAttached(
                "LoadToken",
                typeof(long),
                typeof(CachedCover),
                new PropertyMetadata(0L));

        private static long _nextToken;

        private static async void OnSourceUriChanged(
            DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            long token = ++_nextToken;
            target.SetValue(LoadTokenProperty, token);

            // Clear first: a recycled container must not show the previous
            // row's cover while the new one loads.
            Apply(target, null);

            var uri = args.NewValue as Uri;
            if (uri == null)
            {
                return;
            }

            int decodeWidth = GetDecodeWidth(target);
            if (decodeWidth <= 0)
            {
                return;
            }

            BitmapImage image = await CoverArt.GetBitmapAsync(uri, decodeWidth);
            if (image == null || (long)target.GetValue(LoadTokenProperty) != token)
            {
                return; // failed, or this element was reused in the meantime
            }

            Apply(target, image);
        }

        private static void Apply(DependencyObject target, BitmapImage image)
        {
            var element = target as Image;
            if (element != null)
            {
                element.Source = image;
                return;
            }

            var brush = target as ImageBrush;
            if (brush != null)
            {
                brush.ImageSource = image;
            }
        }
    }
}
