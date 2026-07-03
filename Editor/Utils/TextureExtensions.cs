using UnityEngine;
using UnityEditor;

namespace SmartAuditor.Editor.Utils
{
    internal static class TextureExtensions
    {
        public static void SaveToFile(this Texture2D texture, string path)
        {
            // Get the file extension to determine format
            string extension = System.IO.Path.GetExtension(path).ToLower();
            byte[] encodedTexture;

            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    encodedTexture = texture.EncodeToJPG();
                    break;
                case ".tga":
                    encodedTexture = texture.EncodeToTGA();
                    break;
                case ".exr":
                    encodedTexture = texture.EncodeToEXR();
                    break;
                case ".png":
                default:
                    encodedTexture = texture.EncodeToPNG();
                    break;
            }

            System.IO.File.WriteAllBytes(path, encodedTexture);
            AssetDatabase.Refresh();
        }
    }
}
