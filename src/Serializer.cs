using System;
using System.IO;
using System.Xml.Serialization;


namespace Uber
{
    public static class Serializer
    {
        public static bool FromXml<T>(string filePath, out T config) where T : new()
        {
            config = new T();

            XmlSerializer serializer = null;
            try
            {
                serializer = new XmlSerializer(typeof(T));
            }
            catch(Exception)
            {
                return false;
            }

            bool success = true;
            FileStream reader = null;
            try
            {
                reader = new FileStream(filePath, FileMode.Open);
                config = (T)serializer.Deserialize(reader);
            }
            catch(Exception)
            {
                success = false;
            }
            finally
            {
                if(reader != null)
                {
                    reader.Dispose();
                }
            }

            return success;
        }

        public static bool ToXml<T>(string filePath, T config)
        {
            XmlSerializer serializer = null;
            try
            {
                serializer = new XmlSerializer(typeof(T));
            }
            catch(Exception)
            {
                return false;
            }

            bool success = true;
            StreamWriter writer = null;
            try
            {
                writer = new StreamWriter(filePath);
                serializer.Serialize(writer, config);
            }
            catch(Exception)
            {
                success = false;
            }
            finally
            {
                if(writer != null)
                {
                    writer.Dispose();
                }
            }

            return success;
        }
    }
}