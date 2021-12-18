﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using QQBot.EntityFrameworkCore;

namespace QQBot.EntityFrameworkCore.Migrations
{
    [DbContext(typeof(BotDbContext))]
    [Migration("20211204053126_maimaidx-guess")]
    partial class maimaidxguess
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("ProductVersion", "5.0.7")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("QQBOT.EntityFrameworkCore.Entity.Audit.AuditLog", b =>
                {
                    b.Property<Guid>("EventId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("EventType")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("GroupId")
                        .HasMaxLength(12)
                        .HasColumnType("nvarchar(12)");

                    b.Property<string>("GroupName")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("GroupPermission")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Message")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("Time")
                        .HasColumnType("datetime2");

                    b.Property<string>("UserAlias")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("UserId")
                        .HasMaxLength(12)
                        .HasColumnType("nvarchar(12)");

                    b.Property<string>("UserName")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("EventId");

                    b.ToTable("AuditLog");
                });

            modelBuilder.Entity("QQBOT.EntityFrameworkCore.Entity.Plugin.MaiMaiDx.MaiMaiDxGuess", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("TimesCorrect")
                        .HasColumnType("int");

                    b.Property<int>("TimesStart")
                        .HasColumnType("int");

                    b.Property<int>("TimesWrong")
                        .HasColumnType("int");

                    b.Property<long>("UId")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("UId");

                    b.ToTable("MaiMaiDx.Guess");
                });

            modelBuilder.Entity("QQBOT.EntityFrameworkCore.Entity.Plugin.Timer", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("TimeBegin")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("TimeEnd")
                        .HasColumnType("datetime2");

                    b.Property<long>("Uid")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.ToTable("Timer");
                });
#pragma warning restore 612, 618
        }
    }
}
