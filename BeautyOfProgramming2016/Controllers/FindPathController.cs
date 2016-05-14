using BeautyOfProgramming2016.Models;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web.Http;
using System.IO;
using System.Web;
using System.Threading.Tasks.Dataflow;

namespace BeautyOfProgramming2016.Controllers {
    public class FindPathController : ApiController {
        private static DateTime begin;
        private static TimeSpan expectTime = new TimeSpan(0, 0, 30);
        private static ExecutionDataflowBlockOptions maxThread = 
            new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = -1 };
        public async Task<List<long[]>> GetPath(long id1, long id2) {
            //针对单组数据的debug代码
            //if (id1 != 57898110 || id2 != 2014261844) return new List<long[]>();
            begin = DateTime.Now;
            
            IdType Id1Type = (await IsPossible("composite(AA.AuId=" + id1 + ")")) ? IdType.AuId : IdType.EntityId,
                Id2Type = (await IsPossible("composite(AA.AuId=" + id2 + ")")) ? IdType.AuId : IdType.EntityId;

            BlockingCollection<long[]> ans = new BlockingCollection<long[]>();

            #region 0-hop
            
            var zeroHop = new ActionBlock<int>(async (xxx) => {
                if (Id1Type == IdType.EntityId) {
                    if (Id2Type == IdType.EntityId) {
                        //直接论文引用
                        if (await IsPossible("AND(Id=" + id1 + ",RId=" + id2 + ")")) {
                            ans.Add(new long[] { id1, id2 });
                        }
                    } else if (Id2Type == IdType.AuId) {
                        //论文id1有作者id2
                        if (await IsPossible("AND(Id=" + id1 + ",composite(AA.AuId=" + id2 + "))")) {
                            ans.Add(new long[] { id1, id2 });
                        }
                    }
                } else if (Id1Type == IdType.AuId) {
                    if (Id2Type == IdType.EntityId) {
                        //作者id1参与论文id2
                        if (await IsPossible("AND(Id=" + id2 + ",composite(AA.AuId=" + id1 + "))")) {
                            ans.Add(new long[] { id1, id2 });
                        }
                    } else if (Id2Type == IdType.AuId) {
                        //Impossible
                        #region Error
                        /*
                        //同文章下2个作者
                        if (await IsPossible("AND(composite(AA.AuId=" + id1 + "),composite(AA.AuId=" + id2 + "))")) {
                            ans.Add(new long[] { id1, id2 });

                        } else {
                            //同机构下2个作者
                            bool flag = false;

                            string required = "Id,AA.AuId,AA.AfId";
                            string query = "composite(AA.AuId=" + id1 + ")";
                            AcademicQueryResponse aboutId1 = await DeserializedResult(query, 10000, required);
                            Entity[] entitys = aboutId1.entities;

                            var MainWork = new ActionBlock<Author>(async (y) => {
                                if (DateTime.Now - begin > expectTime) return;
                                bool res = await IsPossible("composite(AND(AA.AfId=" + y.AfId + ",AA.AuId=" + id2 + ")");
                                if (res) {
                                    flag = true;
                                }
                            }, maxThread);
                            if (entitys != null) {
                                foreach (Entity x in entitys) {
                                    if (x.AA != null) {
                                        foreach (Author y in x.AA) {
                                            if (y.AuId == id1 && y.AfId > 0) {
                                                MainWork.Post(y);
                                            }
                                        }
                                    }
                                }
                            }
                            if (flag) {
                                ans.Add(new long[] { id1, id2 });
                            }
                        }
                        */
                        #endregion
                    }
                }
            });
            zeroHop.Post(0);
            
            #endregion

            QueryParam queryParam = new QueryParam { id1Type = Id1Type, id1 = id1, id2Type = Id2Type, id2 = id2 };
            var oneHop = new ActionBlock<QueryParam>(async (x) => {
                List<long[]> t = await OneHopPath(x.id1Type, x.id1, x.id2Type, x.id2);
                foreach (long[] tar in t) {
                    ans.Add(tar);
                }
            });
            oneHop.Post(queryParam);

            var twoHop = new ActionBlock<QueryParam>(async (x) => {
                List<long[]> t = await TwoHopPath(x.id1Type, x.id1, x.id2Type, x.id2);
                foreach (long[] tar in t) {
                    ans.Add(tar);
                }
            });
            twoHop.Post(queryParam);

            zeroHop.Complete();
            oneHop.Complete();
            twoHop.Complete();
            zeroHop.Completion.Wait();
            oneHop.Completion.Wait();
            twoHop.Completion.Wait();

            return ans.ToList();
        }

        private void AddInFront(long x, List<long[]> p, BlockingCollection<long[]>goal) {
            foreach(long[] y in p) {
                long[] z = new long[y.Length + 1];
                y.CopyTo(z, 1);
                z[0] = x;
                goal.Add(z);
            }
        }

        private async Task<List<long[]>> TwoHopPath(IdType id1Type, long id1, IdType id2Type, long id2) {
            BlockingCollection<long[]> ans = new BlockingCollection<long[]>();
            if (DateTime.Now - begin > expectTime) return ans.ToList();

            string queryId1 = id1Type == IdType.AuId ? ("composite(AA.AuId=" + id1 + ")") : ("Id=" + id1);
            string required = id1Type == IdType.AuId ?  "Id,AA.AfId,AA.AuId" : "Id,RId,F.FId,C.CId,J.JId,AA.AuId";

            AcademicQueryResponse aboutId1 = await DeserializedResult(queryId1, 800000, required);
            Entity[] entitys = aboutId1.entities;

            if (id1Type == IdType.EntityId) {
                var eachRId = new ActionBlock<long>(async (rid) => {
                    if (DateTime.Now - begin > expectTime) return;
                    List<long[]> tans = await OneHopPath(IdType.EntityId, rid, id2Type, id2);
                    if (tans.Count > 0) {
                        AddInFront(id1, tans, ans);
                    }
                }, maxThread);

                var eachAuthor = new ActionBlock<Author>(async (y) => {
                    if (DateTime.Now - begin > expectTime) return;
                    List<long[]> tans = await OneHopPath(IdType.AuId, y.AuId, id2Type, id2);
                    if (tans.Count > 0) {
                        AddInFront(id1, tans, ans);
                    }
                }, maxThread);

                var eachField = new ActionBlock<long>(async (x) => {
                    if (DateTime.Now - begin > expectTime) return;
                    List<long[]> tans = await OneHopPath(IdType.FId, x, id2Type, id2);
                    if (tans.Count > 0) {
                        AddInFront(id1, tans, ans);
                    }
                }, maxThread);

                var MainWork = new ActionBlock<Entity>(async (x) => {
                    if (DateTime.Now - begin > expectTime) return;
                    if (x.RId != null) {
                        foreach (long rid in x.RId)
                            eachRId.Post(rid);
                    }
                    if (x.AA != null) {
                        foreach (Author y in x.AA)
                            eachAuthor.Post(y);
                    }
                    if (x.C != null) {
                        List<long[]> tans = await OneHopPath(IdType.CId, x.C.CId, id2Type, id2);
                        if (tans.Count > 0) {
                            AddInFront(id1, tans, ans);
                        }
                    }
                    if (x.J != null) {
                        List<long[]> tans = await OneHopPath(IdType.JId, x.J.JId, id2Type, id2);
                        if (tans.Count > 0) {
                            AddInFront(id1, tans, ans);
                        }
                    }
                    if (x.F != null) {
                        foreach(FieldOfStudy ff in x.F) {
                            eachField.Post(ff.FId);
                        }
                    }
                }, maxThread);
                foreach (Entity x in entitys)
                    MainWork.Post(x);
                MainWork.Complete();
                MainWork.Completion.Wait();
                eachRId.Complete();
                eachAuthor.Complete();
                eachField.Complete();
                eachRId.Completion.Wait();
                eachAuthor.Completion.Wait();
                eachField.Completion.Wait();
            } else if (id1Type == IdType.AuId) {
                var eachAuthor = new ActionBlock<Author>(async (y) => {
                    if (y.AuId == id1 && y.AfId>0) {
                        List<long[]> tans = await OneHopPath(IdType.AfId, y.AfId, id2Type, id2);
                        if (tans.Count > 0) {
                            AddInFront(id1, tans, ans);
                        }
                    }
                }, maxThread);

                var MainWork = new ActionBlock<Entity>(async (x) => {
                    if (DateTime.Now - begin > expectTime) return;
                    if (x.AA != null) {
                        foreach (Author y in x.AA)
                            eachAuthor.Post(y);
                    }
                    List<long[]> tans = await OneHopPath(IdType.EntityId, x.Id, id2Type, id2);
                    if (tans.Count > 0) {
                        AddInFront(id1, tans, ans);
                    }
                }, maxThread);
                foreach (Entity x in entitys)
                    MainWork.Post(x);
                MainWork.Complete();
                MainWork.Completion.Wait();
                eachAuthor.Complete();
                eachAuthor.Completion.Wait();

            }

            return ans.ToList();
        }

        private async Task<List<long[]>> OneHopPath(IdType id1Type, long id1,IdType id2Type, long id2) {
            BlockingCollection<long[]> ans = new BlockingCollection<long[]>();
            if (DateTime.Now - begin > expectTime) return ans.ToList();

            string required = "";
            string query = "";
            if (id2Type == IdType.EntityId) {
                if (id1Type == IdType.EntityId) {
                    //论文id1的引文中有引文id2
                    //或者2篇论文有共同作者/学术领域/期刊/会议
                    //TODO : boost here
                    required = "Id,RId,F.FId,C.CId,J.JId,AA.AuId";
                    query = "Id=" + id1;
                } else if (id1Type == IdType.AuId) {
                    //作者id1的论文中有引文id2
                    required = "Id";
                    query = "AND(composite(AA.AuId=" + id1 + "),RId=" + id2 + ")";
                } else if (id1Type == IdType.AfId) {
                    //机构id1下的作者写了论文id2
                    required = "AA.AuId,AA.AfId";
                    query = "AND(composite(AA.AfId=" + id1 + "),Id=" + id2 + ")";
                } else {//FId,JId,CId
                    //学术领域/期刊/会议id1下的论文有引文id2
                    required = "Id";
                    switch (id1Type) {
                        case IdType.CId:
                            query = "AND(composite(C.CId=" + id1;
                            break;
                        case IdType.FId:
                            query = "AND(composite(F.FId=" + id1;
                            break;
                        case IdType.JId:
                            query = "AND(composite(J.JId=" + id1;
                            break;
                    }
                    query+="),RId=" + id2 + ")";
                }
            } else if (id2Type == IdType.AuId) {
                if (id1Type == IdType.EntityId) {
                    //遍历所有引文，找作者有id2的引文
                    required = "RId";
                    query = "Id=" + id1;
                } else if (id1Type == IdType.AuId) {
                    //同文章下2个作者，或者同机构下2个作者
                    required = "Id,AA.AuId,AA.AfId";
                    query = "composite(AA.AuId=" + id1 + ")";
                } else if (id1Type == IdType.AfId) {
                    // Impossible
                    return ans.ToList();
                } else {//FId,JId,CId
                    //这个会议/期刊/学术领域中有id2作者的论文
                    required = "Id";
                    switch (id1Type) {
                        case IdType.CId:
                            query = "AND(composite(C.CId=" + id1;
                            break;
                        case IdType.FId:
                            query = "AND(composite(F.FId=" + id1;
                            break;
                        case IdType.JId:
                            query = "AND(composite(J.JId=" + id1;
                            break;
                    }
                    query += "),composite(AA.AuId=" + id2 + "))";
                }
            } else {
                return ans.ToList();
            }

            AcademicQueryResponse aboutId1 = await DeserializedResult(query, 800000, required);
            Entity[] entitys = aboutId1.entities;
            if (entitys == null) return ans.ToList();

            HashSet<long> dict = new HashSet<long>();

            if (id2Type == IdType.EntityId) {
                if (id1Type == IdType.EntityId) {
                    //论文id1的引文中有引文id2
                    //或者2篇论文有共同作者/学术领域/期刊/会议
                    #region 论文->x->论文
                    var eachRId = new ActionBlock<long>(async (rid) => {
                        if (DateTime.Now - begin > expectTime) return;
                        if (dict.Contains(rid)) return;
                        lock (dict) {
                            dict.Add(rid);
                        }
                        bool res = await IsPossible("AND(Id=" + rid + ",RId=" + id2 + ")");
                        if (res) {
                            ans.Add(new long[] { id1, rid, id2 });
                        }
                    }, maxThread);

                    var eachAuthor = new ActionBlock<Author>(async (y) => {
                        if (DateTime.Now - begin > expectTime) return;
                        if (dict.Contains(y.AuId)) return;
                        lock (dict) {
                            dict.Add(y.AuId);
                        }
                        bool res = await IsPossible("AND(composite(AA.AuId=" + y.AuId + "),Id=" + id2 + ")");
                        if (res) {
                            ans.Add(new long[] { id1, y.AuId, id2 });
                        }
                    }, maxThread);

                    var eachField = new ActionBlock<long>(async (x) => {
                        if (DateTime.Now - begin > expectTime) return;
                        if (dict.Contains(x)) return;
                        lock (dict) {
                            dict.Add(x);
                        }
                        bool res = await IsPossible("AND(composite(F.FId=" + x + "),Id=" + id2 + ")");
                        if (res) {
                            ans.Add(new long[] { id1, x, id2 });
                        }
                    }, maxThread);

                    var MainWork = new ActionBlock<Entity>(async (x) => {
                        //这篇论文的引文 有引文id2
                        if (DateTime.Now - begin > expectTime) return;
                        if (x.RId != null) {
                            foreach (long rid in x.RId) {
                                eachRId.Post(rid);
                            }
                        }
                        //这篇论文的作者中 有人写了id2
                        if (x.AA != null) {
                            foreach (Author y in x.AA) {
                                eachAuthor.Post(y);
                            }
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
                            foreach(FieldOfStudy xx in x.F) {
                                eachField.Post(xx.FId);
                            }
                        }
                        
                    }, maxThread);
                    if (entitys != null) {
                        foreach (Entity x in entitys) {
                            MainWork.Post(x);
                        }
                    }
                    MainWork.Complete();
                    MainWork.Completion.Wait();
                    eachRId.Complete();
                    eachAuthor.Complete();
                    eachField.Complete();
                    eachRId.Completion.Wait();
                    eachAuthor.Completion.Wait();
                    eachField.Completion.Wait();
                    #endregion
                } else if (id1Type == IdType.AuId) {
                    //作者id1的论文中有引文id2
                    foreach (Entity x in entitys) {
                        ans.Add(new long[] { id1, x.Id, id2 });
                    }
                } else if (id1Type == IdType.AfId) {
                    //机构id1下的作者写了论文id2
                    foreach (Entity x in entitys) {
                        foreach(Author y in x.AA) {
                            if (y.AfId == id1) {
                                ans.Add(new long[] { id1, y.AuId, id2 });
                            }
                        }
                    }
                } else {//FId,JId,CId
                    //学术领域/期刊/会议id1下的论文有引文id2
                    foreach (Entity x in entitys) {
                        ans.Add(new long[] { id1, x.Id, id2 });
                    }
                }
            } else if (id2Type == IdType.AuId) {
                if (id1Type == IdType.EntityId) {

                    var MainWork = new ActionBlock<long>(async (rid) => {
                        if (DateTime.Now - begin > expectTime) return;
                        bool res = await IsPossible("AND(Id=" + rid + ",composite(AA.AuId=" + id2 + "))");
                        if (res) {
                            ans.Add(new long[] { id1, rid, id2 });
                        }
                    }, maxThread);

                    //遍历所有引文，找作者有id2的引文
                    foreach (Entity x in entitys) {
                        foreach (long rid in x.RId) {
                            if (dict.Contains(rid)) continue;
                            dict.Add(rid);
                            MainWork.Post(rid);
                        }
                    }
                    MainWork.Complete();
                    MainWork.Completion.Wait();
                } else if (id1Type == IdType.AuId) {
                    //同文章下2个作者，或者同机构下2个作者
                    #region 作者->x->作者

                    var MainWork = new ActionBlock<Author>(async (y) => {
                        if (DateTime.Now - begin > expectTime) return;
                        bool res = await IsPossible("composite(AND(AA.AfId=" + y.AfId + ",AA.AuId=" + id2 + "))");
                        if (res) {
                            ans.Add(new long[] { id1, y.AfId, id2 });
                        }
                    }, maxThread);

                    foreach (Entity x in entitys) {
                        foreach (Author y in x.AA) {
                            //同文章下2个作者
                            if (y.AuId == id2) {
                                ans.Add(new long[] { id1, x.Id, id2 });
                            }
                            //同机构下2个作者
                            if (y.AuId == id1 && y.AfId>0) {
                                if (dict.Contains(y.AfId)) continue;
                                dict.Add(y.AfId);
                                MainWork.Post(y);
                            }
                        }
                    }
                    MainWork.Complete();
                    MainWork.Completion.Wait();
                    #endregion
                } else if (id1Type == IdType.AfId) {
                    // Impossible
                    return ans.ToList();
                } else {//FId,JId,CId
                    //这个会议/期刊/学术领域中有id2作者的论文
                    foreach (Entity x in entitys) {
                        ans.Add(new long[] { id1, x.Id, id2 });
                    }
                }
            } else {
                return ans.ToList();
            }

            return ans.ToList();
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
        private static bool isFirstTime = true;
        private async Task<AcademicQueryResponse> DeserializedResult(
            string expr,
            long count= 100000,
            string attributes= "Id,RId,F.FId,C.CId,J.JId,AA.AuId,AA.AfId"
            ) {
            //"Id=2140251882";
            //"Composite(AA.AuN == 'jaime teevan')"
            if (isFirstTime) {
                client.DefaultRequestHeaders.Connection.Add("keep-alive");
                isFirstTime = false;
            }
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // Request parameters
            queryString["expr"] = expr;
            queryString["count"] = count.ToString();
            queryString["offset"] = "0";
            queryString["attributes"] = attributes;
            queryString["subscription-key"] = "f7cc29509a8443c5b3a5e56b0e38b5a6";
            var uri = "https://oxfordhk.azure-api.net/academic/v1.0/evaluate?" + queryString;
            AcademicQueryResponse readResult = new AcademicQueryResponse();
            try {
                var response = await client.GetAsync(uri);
                var serializer = new DataContractJsonSerializer(typeof(AcademicQueryResponse));
                var mStream = new MemoryStream(response.Content.ReadAsByteArrayAsync().Result);
                readResult= (AcademicQueryResponse)serializer.ReadObject(mStream);                
            } catch(Exception ex) {
            }
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

    struct QueryParam {
        public IdType id1Type;
        public long id1;
        public IdType id2Type;
        public long id2;
    }
}
