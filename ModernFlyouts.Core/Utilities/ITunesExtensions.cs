#nullable enable

using System;
using System.IO;

using iTunesLib;

namespace ModernFlyouts.Core.Utilities
{
    internal static class ITunesExtensions
    {
        public static string? GetPathToTrackArtwork(this IITTrack? track)
        {
            if (track == null)
                return null;

            var _artwork = track.Artwork?[1];

            if (_artwork == null)
                return "";

            // itunes doesn't provide an easy way to "get" the artwork. We have to save it and then load it again to get it
            string name = $"{track.Name}_{track.Artist}.{_artwork.Format.GetArtworkFileExtension()}";
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalidChar, '_');
            }

            var filepath = Path.Combine(Path.GetTempPath(), name);

            Console.WriteLine("Saved Temp Artwork to " + filepath);
            try
            {
                _artwork.SaveArtworkToFile(filepath);
            }
            catch (UnauthorizedAccessException e)
            {
                Console.WriteLine("File probably already exists");
                Console.WriteLine(e);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return filepath;

        }

        public static string? GetArtworkFileExtension(this ITArtworkFormat format) => format switch
        {
            ITArtworkFormat.ITArtworkFormatBMP  => "bmp",
            ITArtworkFormat.ITArtworkFormatJPEG => "jpeg",
            ITArtworkFormat.ITArtworkFormatPNG  => "png",
            _                                   => "bin",
        };
    }
}
