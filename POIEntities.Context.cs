﻿//------------------------------------------------------------------------------
// <auto-generated>
//    This code was generated from a template.
//
//    Manual changes to this file may cause unexpected behavior in your application.
//    Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace POIProxy
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class poiEntities : DbContext
    {
        public poiEntities()
            : base("name=poiEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public DbSet<activity> activities { get; set; }
        public DbSet<category> categories { get; set; }
        public DbSet<content> contents { get; set; }
        public DbSet<course> courses { get; set; }
        public DbSet<device> devices { get; set; }
        public DbSet<group> groups { get; set; }
        public DbSet<keyword> keywords { get; set; }
        public DbSet<login_attempts> login_attempts { get; set; }
        public DbSet<organization> organizations { get; set; }
        public DbSet<presentation> presentations { get; set; }
        public DbSet<relationship> relationships { get; set; }
        public DbSet<schedule> schedules { get; set; }
        public DbSet<server> servers { get; set; }
        public DbSet<session> sessions { get; set; }
        public DbSet<site> sites { get; set; }
        public DbSet<tag> tags { get; set; }
        public DbSet<tag_relationship> tag_relationship { get; set; }
        public DbSet<token> tokens { get; set; }
        public DbSet<tutor_status> tutor_status { get; set; }
        public DbSet<user_device> user_device { get; set; }
        public DbSet<user_match> user_match { get; set; }
        public DbSet<user_profile> user_profile { get; set; }
        public DbSet<user_right> user_right { get; set; }
        public DbSet<user> users { get; set; }
        public DbSet<users_groups> users_groups { get; set; }
    }
}