﻿using System;
using System.Linq;
using Baseline;
using Marten.Events;
using Marten.Schema;
using Marten.Services;
using Marten.Testing.Documents;
using Marten.Testing.Events;
using Marten.Testing.Fixtures;
using Shouldly;
using Xunit;
using Issue = Marten.Testing.Documents.Issue;

namespace Marten.Testing.Schema
{
    public class DocumentCleanerTests : DocumentSessionFixture<NulloIdentityMap>
    {
        private readonly Lazy<DocumentCleaner> _theCleaner;
        private DocumentCleaner theCleaner => _theCleaner.Value;

        public DocumentCleanerTests()
        {
            _theCleaner = new Lazy<DocumentCleaner>(() => theStore.Advanced.Clean.As<DocumentCleaner>());
        }

        [Fact]
        public void clean_table()
        {
            theSession.Store(new Target { Number = 1 });
            theSession.Store(new Target { Number = 2 });
            theSession.Store(new Target { Number = 3 });
            theSession.Store(new Target { Number = 4 });
            theSession.Store(new Target { Number = 5 });
            theSession.Store(new Target { Number = 6 });

            theSession.SaveChanges();
            theSession.Dispose();

            theCleaner.DeleteDocumentsFor(typeof(Target));

            using (var session = theStore.QuerySession())
            {
                session.Query<Target>().Count().ShouldBe(0);
            }
        }

        [Fact]
        public void delete_all_documents()
        {
            theSession.Store(new Target { Number = 1 });
            theSession.Store(new Target { Number = 2 });
            theSession.Store(new User());
            theSession.Store(new Company());
            theSession.Store(new Issue());

            theSession.SaveChanges();
            theSession.Dispose();

            theCleaner.DeleteAllDocuments();

            using (var session = theStore.QuerySession())
            {
                session.Query<Target>().Count().ShouldBe(0);
                session.Query<User>().Count().ShouldBe(0);
                session.Query<Issue>().Count().ShouldBe(0);
                session.Query<Company>().Count().ShouldBe(0);
            }

        }

        [Fact]
        public void completely_remove_document_type()
        {
            theSession.Store(new Target { Number = 1 });
            theSession.Store(new Target { Number = 2 });

            theSession.SaveChanges();
            theSession.Dispose();

            var tableName = theStore.Schema.MappingFor(typeof(Target)).ToQueryableDocument().Table;

            theStore.Schema.DbObjects.DocumentTables().Contains(tableName)
                .ShouldBeTrue();

            theCleaner.CompletelyRemove(typeof(Target));

            theStore.Schema.DbObjects.DocumentTables().Contains(tableName)
                .ShouldBeFalse();
        }

        [Fact]
        public void completely_remove_document_removes_the_upsert_command_too()
        {
            theSession.Store(new Target { Number = 1 });
            theSession.Store(new Target { Number = 2 });

            theSession.SaveChanges();

            var schema = theStore.Schema;

            var upsertName = schema.MappingFor(typeof(Target)).As<DocumentMapping>().UpsertFunction;

            schema.DbObjects.SchemaFunctionNames().ShouldContain(upsertName);

            theCleaner.CompletelyRemove(typeof(Target));

            schema.DbObjects.SchemaFunctionNames().Contains(upsertName)
                .ShouldBeFalse();
        }

        [Fact]
        public void completely_remove_everything()
        {
            theSession.Store(new Target { Number = 1 });
            theSession.Store(new Target { Number = 2 });
            theSession.Store(new User());
            theSession.Store(new Company());
            theSession.Store(new Issue());

            theSession.SaveChanges();
            theSession.Dispose();

            theCleaner.CompletelyRemoveAll();

            var schema = theStore.Schema;

            ShouldBeEmpty(schema.DbObjects.DocumentTables());
            ShouldBeEmpty(schema.DbObjects.SchemaFunctionNames().Where(x => x.Name != "mt_immutable_timestamp").ToArray());
        }

        [Fact]
        public void delete_all_event_data()
        {
            var streamId = Guid.NewGuid();
            theSession.Events.StartStream<Quest>(streamId, new QuestStarted());

            theSession.SaveChanges();

            theCleaner.DeleteAllEventData();

            theSession.Events.QueryRawEventDataOnly<QuestStarted>().ShouldBeEmpty();
            theSession.Events.FetchStream(streamId).ShouldBeEmpty();

        }

        [Fact]
        public void delete_all_event_data_with_different_schema_names()
        {
            StoreOptions(_ =>
            {
                _.DatabaseSchemaName = "test";
                _.Events.DatabaseSchemaName = "events";
            });

            var streamId = Guid.NewGuid();

            theSession.Events.StartStream<Quest>(streamId, new QuestStarted());

            theSession.SaveChanges();

            theCleaner.DeleteAllEventData();

            // Todo: theSession.Events.Query<>() uses the wrong schema name when using Event type.
            //theSession.Events.Query<QuestStarted>().ShouldBeEmpty();
            theSession.Events.FetchStream(streamId).ShouldBeEmpty();
        }

        private static void ShouldBeEmpty<T>(T[] documentTables)
        {
            var stillInDatabase = string.Join(",", documentTables);
            documentTables.Any().ShouldBeFalse(stillInDatabase);
        }

        [Fact]
        public void delete_except_types()
        {
            theSession.Store(new Target { Number = 1 });
            theSession.Store(new Target { Number = 2 });
            theSession.Store(new User());
            theSession.Store(new Company());
            theSession.Store(new Issue());

            theSession.SaveChanges();
            theSession.Dispose();



            theCleaner.DeleteDocumentsExcept(typeof(Target), typeof(User));


            using (var session = theStore.OpenSession())
            {
                // Not cleaned off
                session.Query<Target>().Count().ShouldBe(2);
                session.Query<User>().Count().ShouldBe(1);

                // Should be cleaned off
                session.Query<Issue>().Count().ShouldBe(0);
                session.Query<Company>().Count().ShouldBe(0);
            }
        }
    }
}