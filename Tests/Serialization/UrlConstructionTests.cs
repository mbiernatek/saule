﻿using System;
using System.Collections.Generic;
using System.Linq;
using Saule.Queries;
using Saule.Serialization;
using Tests.Helpers;
using Xunit;

namespace Tests.Serialization
{
    public class UrlConstructionTests
    {
        [Fact(DisplayName = "Handles query parameters correctly")]
        public void HandlesQueryParams()
        {
            var url = new Uri("http://example.com/api/people/123?a=b&c=d");
            var target = new ResourceSerializer(new Person(prefill: true), new PersonResource(), url, null);
            var result = target.Serialize();

            var jobLinks = result["data"]?["relationships"]?["job"]?["links"];

            var selfLink = result["links"].Value<Uri>("self")?.PathAndQuery;
            var jobSelfLink = jobLinks?.Value<Uri>("self")?.PathAndQuery;
            var jobRelationLink = jobLinks?.Value<Uri>("related")?.PathAndQuery;

            Assert.Equal("/api/people/123?a=b&c=d", selfLink);
            Assert.Equal("/api/people/123/relationships/employer/", jobSelfLink);
            Assert.Equal("/api/people/123/employer/", jobRelationLink);
        }

        [Fact(DisplayName = "Items have self links in a collection")]
        public void SelfLinksInCollection()
        {
            var people = new[]
            {
                new Person(prefill: true, id: "1"),
                new Person(prefill: true, id: "2"),
                new Person(prefill: true, id: "3"),
                new Person(prefill: true, id: "4")
            };
            var target = new ResourceSerializer(
                people, new PersonResource(), new Uri("http://example.com/people/"), null);
            var result = target.Serialize();

            foreach (var elem in result["data"])
            {
                var links = elem["links"];
                Assert.NotNull(links);
                Assert.Equal("/people/" + elem.Value<string>("id") + "/", links.Value<Uri>("self").AbsolutePath);
            }
        }

        [Fact(DisplayName = "Item does not have self link in single element")]
        public void NoSelfLinksInObject()
        {
            var target = new ResourceSerializer(
                new Person(prefill: true), new PersonResource(), new Uri("http://example.com/people/1"), null);
            var result = target.Serialize();

            var links = result["data"]?["links"];

            Assert.Null(links);
        }

        [Fact(DisplayName = "Adds top level self link")]
        public void SelfLink()
        {
            var target = new ResourceSerializer(
                new Person(prefill: true), new PersonResource(), new Uri("http://example.com/people/1"), null);
            var result = target.Serialize();

            var selfLink = result["links"].Value<Uri>("self").AbsolutePath;

            Assert.Equal("/people/1", selfLink);
        }

        [Fact(DisplayName = "Adds next link only if needed")]
        public void NextLink()
        {
            var people = new[]
            {
                new Person(prefill: true, id: "1"),
                new Person(prefill: true, id: "2"),
                new Person(prefill: true, id: "3"),
                new Person(prefill: true, id: "4")
            };
            var target = new ResourceSerializer(
                people, new PersonResource(), new Uri("http://example.com/people/"),
                new PaginationContext(GetQuery("page.number", "2"), perPage:10));
            var result = target.Serialize();

            Assert.Equal(null, result["links"]["next"]);

            target = new ResourceSerializer(
                people, new PersonResource(), new Uri("http://example.com/people/"),
                new PaginationContext(GetQuery("page.number", "2"), perPage:4));
            result = target.Serialize();

            var nextLink = Uri.UnescapeDataString(result["links"].Value<Uri>("next").Query);
            Assert.Equal("?page[number]=3", nextLink);
        }

        [Fact(DisplayName = "Adds previous link only if needed")]
        public void PreviousLink()
        {
            var people = new[]
            {
                new Person(prefill: true, id: "1"),
                new Person(prefill: true, id: "2"),
                new Person(prefill: true, id: "3"),
                new Person(prefill: true, id: "4")
            };
            var target = new ResourceSerializer(
                people, new PersonResource(), new Uri("http://example.com/people/"),
                new PaginationContext(GetQuery("page.number", "0"), perPage:10));
            var result = target.Serialize();

            Assert.Equal(null, result["links"]["prev"]);

            target = new ResourceSerializer(
                people, new PersonResource(), new Uri("http://example.com/people/"),
                new PaginationContext(GetQuery("page.number", "1"), perPage:10));
            result = target.Serialize();

            var nextLink = Uri.UnescapeDataString(result["links"].Value<Uri>("prev").Query);
            Assert.Equal("?page[number]=0", nextLink);
        }

        [Fact(DisplayName = "Keeps other query parameters when paginating")]
        public void PaginationQueryParams()
        {
            var people = new[]
            {
                new Person(prefill: true, id: "1"),
                new Person(prefill: true, id: "2"),
                new Person(prefill: true, id: "3"),
                new Person(prefill: true, id: "4")
            };
            var target = new ResourceSerializer(
                people, new PersonResource(), new Uri("http://example.com/people/?q=a"),
                new PaginationContext(GetQuery("q", "a"), perPage:4));

            var result = target.Serialize();

            var nextLink = Uri.UnescapeDataString(result["links"].Value<Uri>("next").Query);
            Assert.Equal("?q=a&page[number]=1", nextLink);
        }

        [Fact(DisplayName = "Adds first link if paginating")]
        public void FirstLink()
        {
            var people = new[]
            {
                new Person(prefill: true, id: "1"),
                new Person(prefill: true, id: "2"),
                new Person(prefill: true, id: "3"),
                new Person(prefill: true, id: "4")
            };
            var target = new ResourceSerializer(
                people, new PersonResource(), new Uri("http://example.com/people/"),
                new PaginationContext(Enumerable.Empty<KeyValuePair<string, string>>(), perPage:4));

            var result = target.Serialize();

            var nextLink = Uri.UnescapeDataString(result["links"].Value<Uri>("first").Query);
            Assert.Equal("?page[number]=0", nextLink);
        }

        [Fact(DisplayName = "Serializes relationships' links")]
        public void SerializesRelationshipLinks()
        {
            var target = new ResourceSerializer(
                new Person(prefill: true), new PersonResource(), new Uri("http://example.com/people/1"), null);
            var result = target.Serialize();

            var relationships = result["data"]["relationships"];
            var job = relationships["job"];
            var friends = relationships["friends"];

            Assert.Equal("/people/1/employer/", job["links"].Value<Uri>("related").AbsolutePath);
            Assert.Equal("/people/1/relationships/employer/", job["links"].Value<Uri>("self").AbsolutePath);

            Assert.Equal("/people/1/friends/", friends["links"].Value<Uri>("related").AbsolutePath);
            Assert.Equal("/people/1/relationships/friends/", friends["links"].Value<Uri>("self").AbsolutePath);
        }

        [Fact(DisplayName = "Builds absolute links correctly")]
        public void BuildsRightLinks()
        {
            var target = new ResourceSerializer(
                new Person(prefill: true), new PersonResource(), new Uri("http://example.com/api/people/1"), null);
            var result = target.Serialize();

            var job = result["data"]["relationships"]["job"];

            Assert.Equal("http://example.com/api/people/1/employer/",
                job["links"].Value<Uri>("related").ToString());
            Assert.Equal("http://example.com/api/people/1/relationships/employer/",
                job["links"].Value<Uri>("self").ToString());
        }


        private IEnumerable<KeyValuePair<string, string>> GetQuery(string key, string value)
        {
            yield return new KeyValuePair<string, string>(key, value);
        }
    }
}