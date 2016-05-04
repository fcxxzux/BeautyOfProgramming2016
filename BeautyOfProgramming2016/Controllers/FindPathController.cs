using BeautyOfProgramming2016.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web.Http;
using System.IO;
using System.Web;

namespace BeautyOfProgramming2016.Controllers
{
    public class FindPathController : ApiController
    {
        public List<long[]> GetPath(long id1, long id2)
        {
            List<long[]> ans = new List<long[]>();
            ans.Add(new long[] { id1, id2 });
            return ans;
        }

        // /FindPath?entityid=2140251882
        // 如果用了async和await，那么一路上的所有函数必须都是async和await的，不然会卡死
        // 具体没深究，TODO
        // 参见
        // http://stackoverflow.com/questions/10343632/httpclient-getasync-never-returns-when-using-await-async
        public async Task<AcademicQueryResponse> GetSpecificInfo(int entityid)
        {
            return await DeserializedResult("Id=" + entityid);
        }

        private async Task<AcademicQueryResponse> DeserializedResult(string order){
            //"Id=2140251882";
            //"Composite(AA.AuN == 'jaime teevan')"
            var response = await MakeRequest(order, 10000, "Id,RId,F.FId,C.CId,J.JId,AA.AuId,AA.AfId");
            //while (!response.IsCompleted) ;
            string result = response.Content.ReadAsStringAsync().Result;
            var serializer = new DataContractJsonSerializer(typeof(AcademicQueryResponse));
            var mStream = new MemoryStream(response.Content.ReadAsByteArrayAsync().Result);
            AcademicQueryResponse readResult = (AcademicQueryResponse)serializer.ReadObject(mStream);
            return readResult;
        }

        private async Task<HttpResponseMessage> MakeRequest(string expr, long count, string attributes)
        {
            var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // Request parameters
            queryString["expr"] = expr;
            queryString["count"] = count.ToString();
            queryString["offset"] = "0";
            queryString["attributes"] = attributes;
            queryString["subscription-key"] = "f7cc29509a8443c5b3a5e56b0e38b5a6";
            var uri = "https://oxfordhk.azure-api.net/academic/v1.0/evaluate?" + queryString;

            var response = await client.GetAsync(uri);
            return response;
        }
    }
}
