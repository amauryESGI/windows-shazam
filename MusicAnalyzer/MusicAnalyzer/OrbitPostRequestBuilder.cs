using System;
using System.IO;
using System.Net;
using System.Text;

namespace MusicAnalyzer {
    public class OrbitPostRequestBuilder : IRequestBuilder {
        private MemoryStream _requestDataStream = new MemoryStream();

        private string Boundary {
            get;
            set;
        }

        public IceKey Encryptor {
            get;
            private set;
        }

        public char[] Key {
            get;
            private set;
        }

        public OrbitPostRequestBuilder(IceKey encryptor, char[] key) {
            Encryptor = encryptor;
            Key = key;
            Boundary = "AJ8xP50454bf20Gp";
        }

        public void AddEncryptedFile(string name, string fileName, byte[] fileData, int fileSize) {
            Encryptor.Set(Key);
            byte[] numArray = Encryptor.EncBinary(fileData, fileSize);
            AddFile(name, fileName, numArray, numArray.Length);
        }

        public void AddEncryptedParameter(string name, string value) {
            Encryptor.Set(Key);
            char[] chrArray = Encryptor.EncString(value);
            AddParameter(name, new string(chrArray));
        }

        public void AddFile(string name, string fileName, byte[] fileData, int fileSize) {
            string[] newLine = { "--{0}", Environment.NewLine, "Content-Disposition: form-data; name=\"{1}\"; filename=\"{2}\"", Environment.NewLine, "Content-Type: {3}", Environment.NewLine, Environment.NewLine };
            string str = string.Concat(newLine);
            object[] boundary = { Boundary, name, fileName, "application/octet-stream" };
            string str1 = string.Format(str, boundary);
            byte[] bytes = Encoding.UTF8.GetBytes(str1);
            _requestDataStream.Write(bytes, 0, bytes.Length);
            _requestDataStream.Write(fileData, 0, fileSize);
            string newLine1 = Environment.NewLine;
            byte[] numArray = Encoding.UTF8.GetBytes(newLine1);
            _requestDataStream.Write(numArray, 0, numArray.Length);
        }

        public void AddParameter(string name, string value) {
            string[] newLine = { "--{0}", Environment.NewLine, "Content-Disposition: form-data; name=\"{1}\"", Environment.NewLine, Environment.NewLine, "{2}", Environment.NewLine };
            string str = string.Concat(newLine);
            object[] boundary = { Boundary, name, value };
            string str1 = string.Format(str, boundary);
            byte[] bytes = Encoding.UTF8.GetBytes(str1);
            _requestDataStream.Write(bytes, 0, bytes.Length);
        }

        public string MakeRequestUri(string scheme, string hostName, string path) {
            return string.Concat(scheme, "://", hostName, path);
        }

        public void PopulateWebRequestHeaders(WebRequest webRequest) {
            webRequest.Method = "POST";
            webRequest.ContentType = string.Concat("multipart/form-data; boundary=", Boundary);
        }

        public void WriteToRequestStream(Stream requestStream) {
            string str = string.Concat("--", Boundary, "--");
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            _requestDataStream.Write(bytes, 0, bytes.Length);
            byte[] array = _requestDataStream.ToArray();
            requestStream.Write(array, 0, array.Length);
        }
    }

    public interface IRequestBuilder {
        void AddEncryptedFile(string name, string fileName, byte[] fileData, int fileSize);

        void AddEncryptedParameter(string name, string value);

        void AddFile(string name, string fileName, byte[] fileData, int fileSize);

        void AddParameter(string name, string value);

        string MakeRequestUri(string scheme, string hostName, string path);

        void PopulateWebRequestHeaders(WebRequest webRequest);

        void WriteToRequestStream(Stream requestStream);
    }
}
