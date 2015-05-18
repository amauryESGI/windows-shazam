using System.Net;

namespace MusicAnalyzer {
    public struct RequestContext {
        public IRequestBuilder RequestBuilder {
            get;
            set;
        }

        public object State {
            get;
            set;
        }

        public WebRequest WebRequest {
            get;
            set;
        }
    }
}
