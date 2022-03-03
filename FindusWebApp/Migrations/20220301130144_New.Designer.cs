﻿// <auto-generated />
using FindusWebApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace FindusWebApp.Migrations
{
    [DbContext(typeof(TokensContext))]
    [Migration("20220301130144_New")]
    partial class New
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "2.2.4-servicing-10062");

            modelBuilder.Entity("FindusWebApp.Models.Token", b =>
                {
                    b.Property<string>("RealmId")
                        .HasMaxLength(50)
                        .HasColumnType("VARCHAR");

                    b.Property<string>("AccessToken")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("TEXT");

                    b.Property<string>("RefreshToken")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("TEXT");

                    b.Property<int>("ScopeHash")
                        .HasColumnType("INTEGER");

                    b.HasKey("RealmId");

                    b.ToTable("Token");
                });
#pragma warning restore 612, 618
        }
    }
}