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

namespace BeautyOfProgramming2016.Controllers {
    public class FindPathController : ApiController {
        public async Task<List<long[]>> GetPath(long id1, long id2) {
            DateTime begin = DateTime.Now;
            TimeSpan expectTime = new TimeSpan(0, 2, 0);
            List<long[]> ans = new List<long[]>();
            bool isId1Au = await IsPossible("composite(AA.AuId=" + id1 + ")"),
                isId2Au = await IsPossible("composite(AA.AuId=" + id2 + ")");
            string queryId1 = isId1Au ? ("composite(AA.AuId=" + id1 + ")") : ("Id=" + id1);
            string queryId2 = isId2Au ? ("composite(AA.AuId=" + id2 + ")") : ("Id=" + id2);
            AcademicQueryResponse aboutId1 = await DeserializedResult(queryId1); ;
            Entity[] entitys = aboutId1.entities;

            HashSet<long> dict = new HashSet<long>();

            foreach (Entity x in entitys) {
                if (isId1Au) {
                    if (isId2Au) {
                        foreach (Author y in x.AA) {
                            //同文章下2个作者
                            if (y.AuId == id2) {
                                ans.Add(new long[] { id1, x.Id, id2 });
                            }
                            //同机构下2个作者
                            if (y.AuId == id1) {
                                if(dict.Contains(y.AfId))continue;
                                dict.Add( y.AfId);
                                bool res = await IsPossible("composite(AND(AA.AfId=" + y.AfId + ",AA.AuId="+id2 + ")");
                                if (res) {
                                    ans.Add(new long[] { id1, y.AfId, id2 });
                                }
                            }
                            if (DateTime.Now-begin> expectTime) return ans;
                        }
                    }else {
                        //这个作者的论文 引用了id2论文
                        foreach(long rid in x.RId) {
                            if (rid==id2) {
                                ans.Add(new long[] { id1, x.Id, id2 });
                                break;
                            }
                            if (DateTime.Now - begin > expectTime) return ans;
                        }
                        
                    }
                } else {
                    if (isId2Au) {
                        //这篇论文的引文 有作者id2
                        foreach (long rid in x.RId) {
                            if (dict.Contains(rid)) continue;
                            dict.Add(rid);
                            bool res = await IsPossible("AND(Id=" + rid + ",composite(AA.AuId=" + id2 + "))");
                            if (res) {
                                ans.Add(new long[] { id1, rid, id2 });
                            }
                            if (DateTime.Now - begin > expectTime) return ans;
                        }
                    }else {
                        //这篇论文的引文 有引文id2
                        foreach (long rid in x.RId) {
                            if (dict.Contains(rid)) continue;
                            dict.Add(rid);
                            bool res = await IsPossible("AND(Id=" + rid + ",composite(AA.AuId=" + id2 + "))");
                            if (res) {
                                ans.Add(new long[] { id1, rid, id2 });
                            }
                            if (DateTime.Now - begin > expectTime) return ans;
                        }
                        //这篇论文的作者中 有人写了id2
                        foreach (Author y in x.AA) {
                            if (dict.Contains(y.AuId)) continue;
                            dict.Add(y.AuId);
                            bool res = await IsPossible("AND(composite(AA.AuId=" + y.AuId + "),Id=" + id2 + ")");
                            if (res) {
                                ans.Add(new long[] { id1, y.AuId, id2 });
                            }
                            if (DateTime.Now - begin > expectTime) return ans;
                        }
                        //这篇论文的所属期刊/所属会议/所属领域中 有id2
                        if (x.C != null) {
                            bool res = await IsPossible("AND(composite(C.CId=" + x.C.CId + "),Id=" + id2 + ")");
                            if (res) {
                                ans.Add(new long[] { id1, x.C.CId, id2 });
                            }
                        }
                        if (x.J != null) {
                            bool res = await IsPossible("AND(composite(J.JId=" + x.J.JId + "),Id=" + id2 + ")");
                            if (res) {
                                ans.Add(new long[] { id1, x.J.JId, id2 });
                            }
                        }
                        if (x.F != null) {
                            bool res = await IsPossible("AND(composite(F.FId=" + x.F.FId + "),Id=" + id2 + ")");
                            if (res) {
                                ans.Add(new long[] { id1, x.F.FId, id2 });
                            }
                        }
                        if (DateTime.Now - begin > expectTime) return ans;
                    }
                }
            }


            return ans;
        }

        // /FindPath?entityid=2140251882
        // 如果用了async和await，那么一路上的所有函数必须都是async和await的，不然会卡死
        // 具体没深究，TODO
        // 参见
        // http://stackoverflow.com/questions/10343632/httpclient-getasync-never-returns-when-using-await-async
        public async Task<AcademicQueryResponse> GetSpecificInfo(long entityid) {
            return await DeserializedResult("Id=" + entityid);
        }

        private async Task<bool> IsPossible(string expr) {
            AcademicQueryResponse tmp = await DeserializedResult(expr,1);
            return tmp.entities != null && tmp.entities.Length > 0;
        }

        private static HttpClient client=new HttpClient();
        private async Task<AcademicQueryResponse> DeserializedResult(
            string expr,
            long count= 10000,
            string attributes= "Id,RId,F.FId,C.CId,J.JId,AA.AuId,AA.AfId"
            ) {
            //"Id=2140251882";
            //"Composite(AA.AuN == 'jaime teevan')"

            client.DefaultRequestHeaders.Connection.Add("keep-alive");
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // Request parameters
            queryString["expr"] = expr;
            queryString["count"] = count.ToString();
            queryString["offset"] = "0";
            queryString["attributes"] = attributes;
            queryString["subscription-key"] = "f7cc29509a8443c5b3a5e56b0e38b5a6";
            var uri = "https://oxfordhk.azure-api.net/academic/v1.0/evaluate?" + queryString;

            var response = await client.GetAsync(uri);
            
            var serializer = new DataContractJsonSerializer(typeof(AcademicQueryResponse));
            var mStream = new MemoryStream(response.Content.ReadAsByteArrayAsync().Result);
            AcademicQueryResponse readResult = (AcademicQueryResponse)serializer.ReadObject(mStream);
            return readResult;
        }
    }
}
