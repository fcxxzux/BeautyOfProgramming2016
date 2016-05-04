using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace BeautyOfProgramming2016.Models
{
    [DataContract]
    public class AcademicQueryResponse
    {
        [DataMember]
        public string expr;
        [DataMember]
        public Entity[] entities;
    }

    [DataContract]
    public class Entity
    {
        [DataMember]
        public long Id;
        [DataMember]
        public long[] RId;
        [DataMember]
        public Conference C;
        [DataMember]
        public Journal J;
        [DataMember]
        public FieldOfStudy F;
        [DataMember]
        public Author[] AA;
    }

    [DataContract]
    public class Conference
    {
        [DataMember]
        public long CId;
    }

    [DataContract]
    public class Journal
    {
        [DataMember]
        public long JId;
    }

    [DataContract]
    public class FieldOfStudy
    {
        [DataMember]
        public long FId;
    }

    [DataContract]
    public class Author
    {
        [DataMember]
        public long AuId;

        [DataMember]
        public long AfId;
    }
}