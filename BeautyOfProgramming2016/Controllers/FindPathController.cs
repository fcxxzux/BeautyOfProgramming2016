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
            IdType Id1Type = (await IsPossible("composite(AA.AuId=" + id1 + ")")) ? IdType.AuId : IdType.EntityId,
                Id2Type = (await IsPossible("composite(AA.AuId=" + id2 + ")")) ? IdType.AuId : IdType.EntityId;
            string queryId1 = Id1Type == IdType.AuId ? ("composite(AA.AuId=" + id1 + ")") : ("Id=" + id1);
            string required= Id2Type == IdType.AuId? "Id,RId,F.FId,C.CId,J.JId,AA.AuId": "Id,AA.AfId,AA.AuId";

            List<long[]> ans = await OneHopPath(Id1Type,id1, Id2Type,id2);

            AcademicQueryResponse aboutId1 = await DeserializedResult(queryId1,10000,required);
            Entity[] entitys = aboutId1.entities;

            return ans;
        }

        private async Task<List<long[]>> OneHopPath(IdType id1Type, long id1,IdType id2Type, long id2) {
            List<long[]> ans = new List<long[]>();
            HashSet<long> dict = new HashSet<long>();
            string required = "";
            if (id2Type == IdType.EntityId) {
                if (id1Type == IdType.EntityId) {
                    required = "Id,RId,F.FId,C.CId,J.JId,AA.AuId";
                } else if (id1Type == IdType.AuId) {
                    required = "Id,RId";
                }else if (id1Type == IdType.AfId) {
                    required = "AA.AuId";
                } else {//FId,JId,CId
                    required = "Id,RId";
                }
            } else if (id2Type==IdType.AuId) {
                if(id1Type == IdType.EntityId) {
                    required = "Id,RId,F.FId,C.CId,J.JId,AA.AuId";
                } else if (id1Type == IdType.AuId) {
                    required = "Id,RId";
                } else if (id1Type == IdType.AfId) {
                    required = "Id,AuId";
                } else {//FId,JId,CId
                    required = "Id";
                }
            } else return ans;
            if (!isId1Au && !isId2Au) 
            else if (!isId1Au && isId2Au) required = "Id,RId";
            else if (isId1Au && !isId2Au) required = "Id,RId";
            else if (isId1Au && isId2Au) required = "Id,AA.AfId,AA.AuId";

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
                                if (dict.Contains(y.AfId)) continue;
                                dict.Add(y.AfId);
                                bool res = await IsPossible("composite(AND(AA.AfId=" + y.AfId + ",AA.AuId=" + id2 + ")");
                                if (res) {
                                    ans.Add(new long[] { id1, y.AfId, id2 });
                                }
                            }
                            if (DateTime.Now - begin > expectTime) return ans;
                        }
                    } else {
                        //这个作者的论文 引用了id2论文
                        foreach (long rid in x.RId) {
                            if (dict.Contains(rid)) continue;
                            dict.Add(rid);
                            bool res = await IsPossible("AND(Id=" + rid + ",RId=" + id2 + ")");
                            if (res) {
                                ans.Add(new long[] { id1, rid, id2 });
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
                    } else {
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
            AcademicQueryResponse tmp = await DeserializedResult(expr,1,"Id");
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

    enum IdType {
        EntityId,
        AuId,
        AfId,
        CId,
        JId,
        FId
    }
}
