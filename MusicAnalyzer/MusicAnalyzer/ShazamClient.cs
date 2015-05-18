using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Xml.Linq;
using SharedCoreLib.Models.VO;
using SharedCoreLib.Services.ShazamAPI;
using SharedCoreLib.Services.ShazamAPI.Responses;
using SharedCoreLib.Utils.XML;

namespace MusicAnalyzer {
    public class ShazamClient {
        public static string KDoRecognitionUrl = @"http://msft.shazamid.com/orbit/DoRecognition1";
        public static string KRequestResultsUrl = @"http://msft.shazamid.com/orbit/RequestResults1";

        private IceKey _encryptKey = new IceKey(1);
        private ShazamRequest _shazamRequest;
        public event ShazamStateChangedCallback OnRecongnitionStateChanged;
        public string DeviceID { private get; set; }

        public int DoRecognition(byte[] audioBuffer, MicrophoneRecordingOutputFormatType formatType) {
            _shazamRequest = new ShazamRequest();
            ShazamAPIConfig shazamAPIConfig = new ShazamAPIConfig();
            shazamAPIConfig.initKey("20FB1BCBE2C4848F");
            Console.WriteLine(shazamAPIConfig.key);
            _shazamRequest.Token = "B540AD35";
            _shazamRequest.Key = shazamAPIConfig.key;
            _shazamRequest.AudioBuffer = audioBuffer;
            _shazamRequest.Deviceid = "00000000-0000-0000-0000-000000000000";    // It works
            _shazamRequest.Service = "cn=US,cn=V12,cn=SmartClub,cn=ShazamiD,cn=services";
            _shazamRequest.Language = "en-US";
            _shazamRequest.Model = "Microsoft Windows";
            _shazamRequest.Appid = "ShazamId_SmartPhone_Tau__1.3.0";

            if (!string.IsNullOrEmpty(DeviceID))
                _shazamRequest.Deviceid = DeviceID;

            switch (formatType) {
                case MicrophoneRecordingOutputFormatType.PCM: {
                        _shazamRequest.Filename = "sample.wav";
                        break;
                    }
                case MicrophoneRecordingOutputFormatType.MP3: {
                        _shazamRequest.Filename = "sample.mp3";
                        break;
                    }
                case MicrophoneRecordingOutputFormatType.SIG: {
                        _shazamRequest.Filename = "sample.sig";
                        break;
                    }
            }

            ShazamRequest request = _shazamRequest;

            try {
                RaiseOnRecongnitionStateChanged(ShazamRecognitionState.Sending, null);
                byte[] audio = request.AudioBuffer;
                string audioLength = audio.Length.ToString();
                string tagDate = DateTime.Now.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss");
                OrbitPostRequestBuilder orbitPostRequestBuilder = new OrbitPostRequestBuilder(_encryptKey, request.Key);
                orbitPostRequestBuilder.AddEncryptedParameter("cryptToken", request.Token);
                orbitPostRequestBuilder.AddEncryptedParameter("deviceId", request.Deviceid);
                orbitPostRequestBuilder.AddParameter("service", request.Service);
                orbitPostRequestBuilder.AddParameter("language", request.Language);
                orbitPostRequestBuilder.AddEncryptedParameter("deviceModel", request.Model);
                orbitPostRequestBuilder.AddEncryptedParameter("applicationIdentifier", request.Appid);
                orbitPostRequestBuilder.AddEncryptedParameter("tagDate", tagDate);
                orbitPostRequestBuilder.AddEncryptedParameter("sampleBytes", audioLength);
                orbitPostRequestBuilder.AddEncryptedFile("sample", request.Filename, audio, audio.Length);
                WebRequest webRequest = WebRequest.Create(KDoRecognitionUrl);
                orbitPostRequestBuilder.PopulateWebRequestHeaders(webRequest);
                DoTimeoutRequest(new RequestContext {
                    WebRequest = webRequest,
                    RequestBuilder = orbitPostRequestBuilder
                }, RecognitionReadCallback, 30000);
            } catch (Exception e) {
                RecognitionFailed(e);
            }
            return 0;
        }

        private void DoGetResult(ulong requestId) {
            try {
                RaiseOnRecongnitionStateChanged(ShazamRecognitionState.Matching, null);
                _shazamRequest.RequestId = requestId;
                _shazamRequest.ArtWidth = 520;
                ShazamRequest request = _shazamRequest;
                string str = request.RequestId.ToString();
                OrbitPostRequestBuilder orbitPostRequestBuilder = new OrbitPostRequestBuilder(_encryptKey, request.Key);
                orbitPostRequestBuilder.AddEncryptedParameter("cryptToken", request.Token);
                orbitPostRequestBuilder.AddEncryptedParameter("deviceId", request.Deviceid);
                orbitPostRequestBuilder.AddParameter("service", request.Service);
                orbitPostRequestBuilder.AddParameter("language", request.Language);
                orbitPostRequestBuilder.AddEncryptedParameter("deviceModel", request.Model);
                orbitPostRequestBuilder.AddEncryptedParameter("applicationIdentifier", request.Appid);
                orbitPostRequestBuilder.AddEncryptedParameter("coverartSize", request.ArtWidth.ToString());
                orbitPostRequestBuilder.AddEncryptedParameter("requestId", str);
                WebRequest webRequest = WebRequest.Create(KRequestResultsUrl);
                orbitPostRequestBuilder.PopulateWebRequestHeaders(webRequest);
                RequestContext requestContext = new RequestContext
                {
                    WebRequest = webRequest,
                    RequestBuilder = orbitPostRequestBuilder
                };

                DoTimeoutRequest(requestContext, ResultReadCallback, 30000);

            } catch (Exception e) {
                RecognitionFailed(e);
            }
        }

        private void RecognitionReadCallback(IAsyncResult asynchronousResult) {
            try {
                RequestContext asyncState = (RequestContext)asynchronousResult.AsyncState;
                HttpWebRequest webRequest = (HttpWebRequest)asyncState.WebRequest;
                HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.EndGetResponse(asynchronousResult);
                OrbitPostRequestBuilder requestBuilder = asyncState.RequestBuilder as OrbitPostRequestBuilder;

                string responseString = "";
                if (httpWebResponse.GetResponseStream() != null) {
                    using (StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream())) {
                        responseString = streamReader.ReadToEnd();
                    }
                }

                var recognitionShazamResponse = ParseResponseForDoRecognition(responseString);
                if (!string.IsNullOrEmpty(recognitionShazamResponse.errorMessage)) {
                    throw new Exception(recognitionShazamResponse.errorMessage);
                }
                DoGetResult(recognitionShazamResponse.requestId);
            } catch (Exception e) {
                RecognitionFailed(e);
            }
        }

        private void ResultReadCallback(IAsyncResult asynchronousResult) {
            ResultShazamResponse resultShazamResponse = null;
            try {
                RequestContext asyncState = (RequestContext)asynchronousResult.AsyncState;
                HttpWebRequest webRequest = (HttpWebRequest)asyncState.WebRequest;
                HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.EndGetResponse(asynchronousResult);

                string responseString = "";
                if (httpWebResponse.GetResponseStream() != null) {
                    using (StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream())) {
                        responseString = streamReader.ReadToEnd();
                    }
                }

                resultShazamResponse = ParseResponseForRequestResults(responseString);
                if (!string.IsNullOrEmpty(resultShazamResponse.errorMessage)) {
                    throw new Exception(resultShazamResponse.errorMessage);
                }
                RaiseOnRecongnitionStateChanged(ShazamRecognitionState.Done, new ShazamResponse(resultShazamResponse));

            } catch (Exception e) {
                RecognitionFailed(e);
            }
        }

        private void RaiseOnRecongnitionStateChanged(ShazamRecognitionState state, ShazamResponse response) {
            if (OnRecongnitionStateChanged != null)
                RaiseEventOnUIThread(OnRecongnitionStateChanged, new Object[] { state, response });
        }

        private void RecognitionFailed(Exception e) {
            if (OnRecongnitionStateChanged != null)
                RaiseEventOnUIThread(OnRecongnitionStateChanged, new Object[] { ShazamRecognitionState.Failed, new ShazamResponse(e) });
        }

        private void RaiseEventOnUIThread(Delegate theEvent, object[] args) {
            try {
                foreach (Delegate d in theEvent.GetInvocationList()) {
                    ISynchronizeInvoke syncer = d.Target as ISynchronizeInvoke;
                    if (syncer == null) {
                        d.DynamicInvoke(args);
                    } else {
                        Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-us");
                        syncer.BeginInvoke(d, args);  // cleanup omitted
                    }
                }
            } catch {
                OnRecongnitionStateChanged((ShazamRecognitionState)args[0], (ShazamResponse)args[1]);
            }
        }

        private ResultShazamResponse ParseResponseForRequestResults(string responseString) {
            ResultShazamResponse resultShazamResponse = new ResultShazamResponse();
            XDocument xDocument = XDocument.Parse(responseString);
            XNamespace xNamespace = "http://orbit.shazam.com/v1/response";
            XElement elementIgnoreNamespace = xDocument.Root.GetElementIgnoreNamespace(xNamespace, "requestResults1");
            XElement xElement = elementIgnoreNamespace;
            if (elementIgnoreNamespace == null) {
                XElement elementIgnoreNamespace1 = xDocument.Root.GetElementIgnoreNamespace(xNamespace, "error");
                XElement xElement1 = elementIgnoreNamespace1;
                if (elementIgnoreNamespace1 != null) {
                    resultShazamResponse.errorCode = int.Parse(xElement1.Attribute("code").Value);
                }
            } else {
                XElement elementIgnoreNamespace2 = xElement.GetElementIgnoreNamespace(xNamespace, "request");
                TagVO tagVO = new TagVO();
                tagVO.Id = elementIgnoreNamespace2.Attribute("requestId").Value;
                resultShazamResponse.newTag = tagVO;
                TrackVO trackVO = null;
                int num = 0;
                XElement xElement2 = xElement.GetElementIgnoreNamespace(xNamespace, "tracks");
                XElement elementIgnoreNamespace3 = xElement2.GetElementIgnoreNamespace(xNamespace, "track");
                if (elementIgnoreNamespace3 != null) {
                    trackVO = ParseXmlElementForTrackData(xNamespace, elementIgnoreNamespace3, false);
                    if (elementIgnoreNamespace3.Attribute("cache-max-age") != null) {
                        num = Convert.ToInt32(elementIgnoreNamespace3.Attribute("cache-max-age").Value);
                    }
                }
                if (trackVO != null) {
                    resultShazamResponse.newTag.Track = trackVO;
                }
            }
            return resultShazamResponse;
        }

        private RecognitionShazamResponse ParseResponseForDoRecognition(string responseString) {
            RecognitionShazamResponse recognitionShazamResponse = new RecognitionShazamResponse();
            XDocument xDocument = XDocument.Parse(responseString);
            XNamespace xNamespace = "http://orbit.shazam.com/v1/response";
            XElement elementIgnoreNamespace = xDocument.Root.GetElementIgnoreNamespace(xNamespace, "doRecognition1");
            XElement xElement = elementIgnoreNamespace;
            if (elementIgnoreNamespace == null) {
                XElement elementIgnoreNamespace1 = xDocument.Root.GetElementIgnoreNamespace(xNamespace, "error");
                XElement xElement1 = elementIgnoreNamespace1;
                if (elementIgnoreNamespace1 != null) {
                    recognitionShazamResponse.errorCode = int.Parse(xElement1.Attribute("code").Value);
                }
            } else {
                XElement elementIgnoreNamespace2 = xElement.GetElementIgnoreNamespace(xNamespace, "requestId");
                XElement xElement2 = elementIgnoreNamespace2;
                if (elementIgnoreNamespace2 != null) {
                    recognitionShazamResponse.requestId = ulong.Parse(xElement2.Attribute("id").Value);
                }
            }
            return recognitionShazamResponse;
        }

        private bool DoTimeoutRequest(RequestContext requestContext, AsyncCallback callback, int millisecondsTimeout) {
            try {
                bool flag = false;
                Timer timer = null;
                TimerCallback timerCallback = null;
                timerCallback = (object state) => {
                    lock (timer) {
                        if (!flag) {
                            flag = true;
                            requestContext.WebRequest.Abort();
                        }
                    }
                };
                AsyncCallback asyncCallback = (IAsyncResult ar) => {
                    lock (timer) {
                        if (!flag) {
                            flag = true;
                            timer.Change(-1, -1);
                        }
                    }
                    callback(ar);
                };
                if (!requestContext.WebRequest.Method.Equals("POST")) {
                    requestContext.WebRequest.BeginGetResponse(asyncCallback, requestContext);
                } else {
                    AsyncCallback asyncCallback1 = (IAsyncResult ar) => {
                        requestContext = (RequestContext)ar.AsyncState;
                        using (Stream stream = requestContext.WebRequest.EndGetRequestStream(ar)) {
                            requestContext.RequestBuilder.WriteToRequestStream(stream);
                        }
                        requestContext.WebRequest.BeginGetResponse(asyncCallback, requestContext);
                    };
                    requestContext.WebRequest.BeginGetRequestStream(asyncCallback1, requestContext);

                }
                timer = new Timer(timerCallback, null, millisecondsTimeout, -1);
            } catch (Exception exception) {
                throw new IOException(string.Empty, exception);
            }
            return true;
        }

        private TrackVO ParseXmlElementForTrackData(XNamespace xNamespace, XElement trackElem, bool fromList = false) {
            TrackVO trackVO = new TrackVO
            {
                Id = Convert.ToInt32(trackElem.Attribute("id").Value),
                Title = trackElem.GetElementIgnoreNamespace(xNamespace, "ttitle").Value
            };

            XElement elementIgnoreNamespace = trackElem.GetElementIgnoreNamespace(xNamespace, "tartists");
            if (elementIgnoreNamespace != null) {
                XElement xElement = elementIgnoreNamespace.GetElementIgnoreNamespace(xNamespace, "tartist");
                if (xElement != null) {
                    trackVO.Artist = xElement.Value;
                }
            }
            XElement elementIgnoreNamespace1 = trackElem.GetElementIgnoreNamespace(xNamespace, "tlabel");
            if (elementIgnoreNamespace1 != null) {
                trackVO.Label = elementIgnoreNamespace1.Value;
            }
            XElement xElement1 = trackElem.GetElementIgnoreNamespace(xNamespace, "tgenre");
            if (xElement1 != null) {
                XElement elementIgnoreNamespace2 = xElement1.GetElementIgnoreNamespace(xNamespace, "tparentGenre");
                if (elementIgnoreNamespace2 != null) {
                    trackVO.Genre = elementIgnoreNamespace2.Value;
                }
            }
            XElement xElement2 = trackElem.GetElementIgnoreNamespace(xNamespace, "tcoverart");
            if (xElement2 != null) {
                trackVO.ImageUri = xElement2.Value;
            }
            XElement elementIgnoreNamespace3 = trackElem.GetElementIgnoreNamespace(xNamespace, "addOns");
            if (elementIgnoreNamespace3 != null) {
                foreach (XElement xElement3 in elementIgnoreNamespace3.Elements(xNamespace + "addOn")) {
                    if (xElement3.Attribute("providerName").Value == "Zune") {
                        XElement elementIgnoreNamespace4 = xElement3.GetElementIgnoreNamespace(xNamespace, "actions");
                        if (elementIgnoreNamespace4 != null) {
                            XElement xElement4 = elementIgnoreNamespace4.GetElementIgnoreNamespace(xNamespace, "MarketplaceSearchTask");
                            if (xElement4 != null) {
                                trackVO.ContentType = xElement4.Attribute("ContentType").Value;
                                trackVO.SearchTerms = xElement4.Attribute("SearchTerms").Value;
                            }
                        }
                        XElement elementIgnoreNamespace5 = xElement3.GetElementIgnoreNamespace(xNamespace, "content");
                        if (elementIgnoreNamespace5 == null) {
                            continue;
                        }
                        trackVO.PurchaseUrl = elementIgnoreNamespace5.Value;
                    } else if (xElement3.Attribute("providerName").Value != "Share") {
                        XElement xElement5 = xElement3.GetElementIgnoreNamespace(xNamespace, "actions");
                        if (xElement5 == null) {
                            continue;
                        }
                        AddOnVO addOnVO = new AddOnVO();
                        addOnVO.ProviderName = xElement3.Attribute("providerName").Value;
                        addOnVO.Caption = xElement3.Attribute("typeName").Value;
                        int num = -1;
                        if (int.TryParse(xElement3.Attribute("typeId").Value, out num)) {
                            addOnVO.TypeId = num;
                        }
                        int num1 = -1;
                        if (xElement3.Attribute("creditTypeId") != null && int.TryParse(xElement3.Attribute("creditTypeId").Value, out num1)) {
                            addOnVO.CreditTypeId = num1;
                        }
                        addOnVO.Actions = new List<AddOnActionVO>();
                        foreach (XElement xElement6 in xElement5.Elements()) {
                            AddOnActionVO addOnActionVO = new AddOnActionVO();
                            addOnActionVO.Url = xElement6.Attribute("Uri").Value;
                            string localName = xElement6.Name.LocalName;
                            string str = localName;
                            if (localName != null) {
                                if (str == "LaunchUriTask") {
                                    addOnActionVO.Type = AddOnActionVO.ActionType.LaunchUri;
                                } else if (str == "WebViewTask") {
                                    addOnActionVO.Type = AddOnActionVO.ActionType.WebView;
                                }
                            }
                            addOnVO.Actions.Add(addOnActionVO);
                        }
                        string providerName = addOnVO.ProviderName;
                        string str1 = providerName;
                        if (providerName != null) {
                            switch (str1) {
                                case "Buy": {
                                        addOnVO.ImageUri = "ms-appx:///PresentationLib/Assets/buy.png";
                                        break;
                                    }
                                case "YouTube": {
                                        addOnVO.ImageUri = "ms-appx:///PresentationLib/Assets/youtube.png";
                                        break;
                                    }
                                case "Biography": {
                                        addOnVO.ImageUri = "ms-appx:///PresentationLib/Assets/biog.png";
                                        break;
                                    }
                                case "Discography": {
                                        addOnVO.ImageUri = "ms-appx:///PresentationLib/Assets/discog.png";
                                        break;
                                    }
                                case "ProductReview": {
                                        addOnVO.ImageUri = "ms-appx:///PresentationLib/Assets/reviews.png";
                                        break;
                                    }
                                case "TrackReview": {
                                        addOnVO.ImageUri = "ms-appx:///PresentationLib/Assets/trackreview.png";
                                        break;
                                    }
                                case "ShazamLyrics": {
                                        addOnVO.ImageUri = "ms-appx:///PresentationLib/Assets/lyrics.png";
                                        break;
                                    }
                                case "Recommendations": {
                                        addOnVO.ImageUri = "ms-appx:///PresentationLib/Assets/recommendations.png";
                                        break;
                                    }
                            }
                        }
                        addOnVO.AssociateOwnerTrack(trackVO);
                        trackVO.AddOns = new List<AddOnVO> { addOnVO };
                    } else {
                        XElement elementIgnoreNamespace6 = xElement3.GetElementIgnoreNamespace(xNamespace, "actions");
                        if (elementIgnoreNamespace6 == null) {
                            continue;
                        }
                        XElement elementIgnoreNamespace7 = elementIgnoreNamespace6.GetElementIgnoreNamespace(xNamespace, "ShareLinkTask");
                        if (elementIgnoreNamespace7 == null) {
                            continue;
                        }
                        trackVO.ShareLinkUri = elementIgnoreNamespace7.Attribute("LinkUri").Value;
                        trackVO.ShareLinkTitle = elementIgnoreNamespace7.Attribute("Title").Value;
                        trackVO.ShareLinkMessage = elementIgnoreNamespace7.Attribute("Message").Value;
                    }
                }
            }
            XElement xElement7 = trackElem.GetElementIgnoreNamespace(xNamespace, "tproduct");
            if (xElement7 != null) {
                trackVO.Product = xElement7.Value;
            }
            return trackVO;
        }
    }

    public class ShazamResponse {
        public TagVO Tag { get; private set; }
        public Exception Exception { get; private set; }

        public ShazamResponse(ResultShazamResponse result) {
            if (result.newTag != null)
                Tag = result.newTag;
        }

        public ShazamResponse(Exception exception) {
            Exception = exception;
        }
    }

    public enum MicrophoneRecordingOutputFormatType {
        PCM,
        MP3,
        SIG
    }

    public enum ShazamRecognitionState {
        Sending,
        Matching,
        Done,
        Failed
    }

    public delegate void ShazamStateChangedCallback(ShazamRecognitionState state, ShazamResponse response);
}
