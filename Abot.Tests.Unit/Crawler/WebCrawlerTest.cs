﻿using Abot.Core;
using Abot.Crawler;
using Abot.Poco;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Abot.Tests.Unit.Crawler
{
    [TestFixture]
    public class CrawlerBasicTest
    {
        WebCrawler _unitUnderTest;
        Mock<IPageRequester> _fakeHttpRequester;
        Mock<IHyperLinkParser> _fakeHyperLinkParser;
        Mock<ICrawlDecisionMaker> _fakeCrawlDecisionMaker;
        Mock<IDomainRateLimiter> _fakeDomainRateLimiter;
        FifoScheduler _dummyScheduler;
        ThreadManager _dummyThreadManager;
        CrawlConfiguration _dummyConfiguration;
        Uri _rootUri;

        [SetUp]
        public void SetUp()
        {
            _fakeHyperLinkParser = new Mock<IHyperLinkParser>();
            _fakeHttpRequester = new Mock<IPageRequester>();
            _fakeCrawlDecisionMaker = new Mock<ICrawlDecisionMaker>();
            _fakeDomainRateLimiter = new Mock<IDomainRateLimiter>();

            _dummyScheduler = new FifoScheduler();
            _dummyThreadManager = new ThreadManager(1);
            _dummyConfiguration = new CrawlConfiguration();
            _dummyConfiguration.ConfigurationExtensions.Add("somekey", "someval");

            _unitUnderTest = new WebCrawler(_dummyThreadManager, _dummyScheduler, _fakeHttpRequester.Object, _fakeHyperLinkParser.Object, _fakeCrawlDecisionMaker.Object, _fakeDomainRateLimiter.Object, _dummyConfiguration);

            _rootUri = new Uri("http://a.com/");
        }

        [Test]
        public void Constructor_Empty()
        {
            Assert.IsNotNull(new WebCrawler());
        }

        [Test]
        public void Constructor_WithConfiguration()
        {
            Assert.IsNotNull(new WebCrawler(new CrawlConfiguration()));
        }

        [Test]
        public void Constructor_WithDecisionMaker()
        {
            Assert.IsNotNull(new WebCrawler(new CrawlDecisionMaker()));
        }

        [Test]
        public void Constructor_WithDecisionMakerAndConfiguration()
        {
            Assert.IsNotNull(new WebCrawler(new CrawlDecisionMaker(), new CrawlConfiguration()));
        }


        [Test]
        public void Crawl_CallsDependencies()
        {
            Uri uri1 = new Uri(_rootUri.AbsoluteUri + "a.html");
            Uri uri2 = new Uri(_rootUri.AbsoluteUri + "b.html");

            CrawledPage homePage = new CrawledPage(_rootUri) { RawContent = "content here"};
            CrawledPage page1 = new CrawledPage(uri1);
            CrawledPage page2 = new CrawledPage(uri2);

            List<Uri> links = new List<Uri>{uri1, uri2};

            _fakeHttpRequester.Setup(f => f.MakeRequest(_rootUri, It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(homePage);
            _fakeHttpRequester.Setup(f => f.MakeRequest(uri1, It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(page1);
            _fakeHttpRequester.Setup(f => f.MakeRequest(uri2, It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(page2);
            _fakeHyperLinkParser.Setup(f => f.GetLinks(_rootUri, It.IsAny<string>())).Returns(links);
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision{Allow = true});
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision{ Allow = true });

            _unitUnderTest.Crawl(_rootUri);

            _fakeHttpRequester.Verify(f => f.MakeRequest(_rootUri, It.IsAny<Func<CrawledPage, CrawlDecision>>()), Times.Once());
            _fakeHttpRequester.Verify(f => f.MakeRequest(uri1, It.IsAny<Func<CrawledPage, CrawlDecision>>()), Times.Once());
            _fakeHttpRequester.Verify(f => f.MakeRequest(uri2, It.IsAny<Func<CrawledPage, CrawlDecision>>()), Times.Once());
            _fakeHyperLinkParser.Verify(f => f.GetLinks(_rootUri, It.IsAny<string>()), Times.Once());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>()), Times.Exactly(3));
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>()), Times.Exactly(3));
            _fakeDomainRateLimiter.Verify(f => f.RateLimit(It.IsAny<Uri>()), Times.Never());
        }

        [Test]
        public void Crawl_MinCrawlDelayGreaterThanZero_CallsDependenciesAlongWithDomainRateLimiter()
        {
            Uri uri1 = new Uri(_rootUri.AbsoluteUri + "a.html");
            Uri uri2 = new Uri(_rootUri.AbsoluteUri + "b.html");

            CrawledPage homePage = new CrawledPage(_rootUri) { RawContent = "content here" };
            CrawledPage page1 = new CrawledPage(uri1);
            CrawledPage page2 = new CrawledPage(uri2);

            List<Uri> links = new List<Uri> { uri1, uri2 };

            _fakeHttpRequester.Setup(f => f.MakeRequest(_rootUri, It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(homePage);
            _fakeHttpRequester.Setup(f => f.MakeRequest(uri1, It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(page1);
            _fakeHttpRequester.Setup(f => f.MakeRequest(uri2, It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(page2);
            _fakeHyperLinkParser.Setup(f => f.GetLinks(_rootUri, It.IsAny<string>())).Returns(links);
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });

            _dummyConfiguration.MinCrawlDelayPerDomainMilliSeconds = 50;//BY HAVING A CRAWL DELAY ABOVE ZERO WE EXPECT THE IDOMAINRATELIMITER TO BE CALLED
            _unitUnderTest = new WebCrawler(_dummyThreadManager, _dummyScheduler, _fakeHttpRequester.Object, _fakeHyperLinkParser.Object, _fakeCrawlDecisionMaker.Object, _fakeDomainRateLimiter.Object, _dummyConfiguration);

            _unitUnderTest.Crawl(_rootUri);

            _fakeHttpRequester.Verify(f => f.MakeRequest(_rootUri, It.IsAny<Func<CrawledPage, CrawlDecision>>()), Times.Once());
            _fakeHttpRequester.Verify(f => f.MakeRequest(uri1, It.IsAny<Func<CrawledPage, CrawlDecision>>()), Times.Once());
            _fakeHttpRequester.Verify(f => f.MakeRequest(uri2, It.IsAny<Func<CrawledPage, CrawlDecision>>()), Times.Once());
            _fakeHyperLinkParser.Verify(f => f.GetLinks(_rootUri, It.IsAny<string>()), Times.Once());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>()), Times.Exactly(3));
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>()), Times.Exactly(3));
            _fakeDomainRateLimiter.Verify(f => f.RateLimit(It.IsAny<Uri>()), Times.Exactly(3));//BY HAVING A CRAWL DELAY ABOVE ZERO WE EXPECT THE IDOMAINRATELIMITER TO BE CALLED
        }

        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Crawl_NullUri()
        {
            _unitUnderTest.Crawl(null);
        }

        #region Synchronous Event Tests

        [Test]
        public void Crawl_CrawlDecisionMakerMethodsReturnTrue_PageCrawlStartingAndCompletedEventsFires()
        {
            _fakeHttpRequester.Setup(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(new CrawledPage(_rootUri));
            _fakeHyperLinkParser.Setup(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>())).Returns(new List<Uri>());
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });

            int _pageCrawlStartingCount = 0;
            int _pageCrawlCompletedCount = 0;
            int _pageCrawlDisallowedCount = 0;
            int _pageLinksCrawlDisallowedCount = 0;
            _unitUnderTest.PageCrawlCompleted += (s, e) => ++_pageCrawlCompletedCount;
            _unitUnderTest.PageCrawlStarting += (s, e) => ++_pageCrawlStartingCount;
            _unitUnderTest.PageCrawlDisallowed += (s, e) => ++_pageCrawlDisallowedCount;
            _unitUnderTest.PageLinksCrawlDisallowed += (s, e) => ++_pageLinksCrawlDisallowedCount;

            _unitUnderTest.Crawl(_rootUri);
            System.Threading.Thread.Sleep(100);//sleep since the events are async and may not complete before returning

            _fakeHttpRequester.Verify(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>()), Times.Once());
            _fakeHyperLinkParser.Verify(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>()), Times.Once());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>()), Times.Once());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>()), Times.Once());
            _fakeDomainRateLimiter.Verify(f => f.RateLimit(It.IsAny<Uri>()), Times.Never());

            Assert.AreEqual(1, _pageCrawlStartingCount);
            Assert.AreEqual(1, _pageCrawlCompletedCount);
            Assert.AreEqual(0, _pageCrawlDisallowedCount);
            Assert.AreEqual(0, _pageLinksCrawlDisallowedCount);
        }

        [Test]
        public void Crawl_CrawlDecisionMakerShouldCrawlLinksMethodReturnsFalse_PageLinksCrawlDisallowedEventFires()
        {
            _fakeHttpRequester.Setup(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(new CrawledPage(_rootUri));
            _fakeHyperLinkParser.Setup(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>())).Returns(new List<Uri>());
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = false, Reason = "aaa" });

            int _pageCrawlStartingCount = 0;
            int _pageCrawlCompletedCount = 0;
            int _pageCrawlDisallowedCount = 0;
            int _pageLinksCrawlDisallowedCount = 0;
            _unitUnderTest.PageCrawlCompleted += (s, e) => ++_pageCrawlCompletedCount;
            _unitUnderTest.PageCrawlStarting += (s, e) => ++_pageCrawlStartingCount;
            _unitUnderTest.PageCrawlDisallowed += (s, e) => ++_pageCrawlDisallowedCount;
            _unitUnderTest.PageLinksCrawlDisallowed += (s, e) => ++_pageLinksCrawlDisallowedCount;

            _unitUnderTest.Crawl(_rootUri);
            System.Threading.Thread.Sleep(100);//sleep since the events are async and may not complete before returning

            _fakeHttpRequester.Verify(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>()), Times.Once());
            _fakeHyperLinkParser.Verify(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>()), Times.Never());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>()), Times.Once());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>()), Times.Once());
            _fakeDomainRateLimiter.Verify(f => f.RateLimit(It.IsAny<Uri>()), Times.Never());

            Assert.AreEqual(1, _pageCrawlStartingCount);
            Assert.AreEqual(1, _pageCrawlCompletedCount);
            Assert.AreEqual(0, _pageCrawlDisallowedCount);
            Assert.AreEqual(1, _pageLinksCrawlDisallowedCount);
        }

        [Test]
        public void Crawl_CrawlDecisionMakerShouldCrawlMethodReturnsFalse_PageCrawlDisallowedEventFires()
        {
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = false, Reason = "aaa" });

            int _pageCrawlStartingCount = 0;
            int _pageCrawlCompletedCount = 0;
            int _pageCrawlDisallowedCount = 0;
            int _pageLinksCrawlDisallowedCount = 0;
            _unitUnderTest.PageCrawlCompleted += (s, e) => ++_pageCrawlCompletedCount;
            _unitUnderTest.PageCrawlStarting += (s, e) => ++_pageCrawlStartingCount;
            _unitUnderTest.PageCrawlDisallowed += (s, e) => ++_pageCrawlDisallowedCount;
            _unitUnderTest.PageLinksCrawlDisallowed += (s, e) => ++_pageLinksCrawlDisallowedCount;

            _unitUnderTest.Crawl(_rootUri);
            System.Threading.Thread.Sleep(100);//sleep since the events are async and may not complete before returning

            _fakeHttpRequester.Verify(f => f.MakeRequest(It.IsAny<Uri>()), Times.Never());
            _fakeHyperLinkParser.Verify(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>()), Times.Never());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>()), Times.Once());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>()), Times.Never());
            _fakeDomainRateLimiter.Verify(f => f.RateLimit(It.IsAny<Uri>()), Times.Never());

            Assert.AreEqual(0, _pageCrawlStartingCount);
            Assert.AreEqual(0, _pageCrawlCompletedCount);
            Assert.AreEqual(1, _pageCrawlDisallowedCount);
            Assert.AreEqual(0, _pageLinksCrawlDisallowedCount);
        }


        [Test]
        public void Crawl_PageCrawlStartingAndCompletedEventSubscriberThrowsExceptions_DoesNotCrash()
        {
            _fakeHttpRequester.Setup(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(new CrawledPage(_rootUri));
            _fakeHyperLinkParser.Setup(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>())).Returns(new List<Uri>());
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });

            FifoScheduler _dummyScheduler = new FifoScheduler();
            ThreadManager _dummyThreadManager = new ThreadManager(1);
            _unitUnderTest = new WebCrawler(_dummyThreadManager, _dummyScheduler, _fakeHttpRequester.Object, _fakeHyperLinkParser.Object, _fakeCrawlDecisionMaker.Object, _fakeDomainRateLimiter.Object, new CrawlConfiguration());

            int _pageCrawlStartingCount = 0;
            int _pageCrawlCompletedCount = 0;
            int _pageCrawlDisallowedCount = 0;
            int _pageLinksCrawlDisallowedCount = 0;
            _unitUnderTest.PageCrawlCompleted += (s, e) => ++_pageCrawlCompletedCount;
            _unitUnderTest.PageCrawlStarting += (s, e) => ++_pageCrawlStartingCount;
            _unitUnderTest.PageCrawlStarting += new EventHandler<PageCrawlStartingArgs>(ThrowExceptionWhen_PageCrawlStarting);
            _unitUnderTest.PageCrawlCompleted += new EventHandler<PageCrawlCompletedArgs>(ThrowExceptionWhen_PageCrawlCompleted);
            _unitUnderTest.PageCrawlDisallowed += (s, e) => ++_pageCrawlDisallowedCount;
            _unitUnderTest.PageLinksCrawlDisallowed += (s, e) => ++_pageLinksCrawlDisallowedCount;

            _unitUnderTest.Crawl(_rootUri);
            System.Threading.Thread.Sleep(1000);//sleep since the events are async and may not complete

            _fakeHttpRequester.Verify(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>()), Times.Once());
            _fakeHyperLinkParser.Verify(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>()), Times.Once());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>()), Times.Once());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>()), Times.Once());
            _fakeDomainRateLimiter.Verify(f => f.RateLimit(It.IsAny<Uri>()), Times.Never());

            Assert.AreEqual(1, _pageCrawlStartingCount);
            Assert.AreEqual(1, _pageCrawlCompletedCount);
            Assert.AreEqual(0, _pageCrawlDisallowedCount);
            Assert.AreEqual(0, _pageLinksCrawlDisallowedCount);
        }

        [Test]
        public void Crawl_PageCrawlDisallowedSubscriberThrowsExceptions_DoesNotCrash()
        {
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = false, Reason = "aaa" });

            int _pageCrawlStartingCount = 0;
            int _pageCrawlCompletedCount = 0;
            int _pageCrawlDisallowedCount = 0;
            int _pageLinksCrawlDisallowedCount = 0;
            _unitUnderTest.PageCrawlCompleted += (s, e) => ++_pageCrawlCompletedCount;
            _unitUnderTest.PageCrawlStarting += (s, e) => ++_pageCrawlStartingCount;
            _unitUnderTest.PageCrawlDisallowed += (s, e) => ++_pageCrawlDisallowedCount;
            _unitUnderTest.PageLinksCrawlDisallowed += (s, e) => ++_pageLinksCrawlDisallowedCount;
            _unitUnderTest.PageCrawlDisallowed += new EventHandler<PageCrawlDisallowedArgs>(ThrowExceptionWhen_PageCrawlDisallowed);
            _unitUnderTest.PageLinksCrawlDisallowed += new EventHandler<PageLinksCrawlDisallowedArgs>(ThrowExceptionWhen_PageLinksCrawlDisallowed);

            _unitUnderTest.Crawl(_rootUri);
            System.Threading.Thread.Sleep(1000);//sleep since the events are async and may not complete

            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>()), Times.Once());

            Assert.AreEqual(0, _pageCrawlStartingCount);
            Assert.AreEqual(0, _pageCrawlCompletedCount);
            Assert.AreEqual(1, _pageCrawlDisallowedCount);
            Assert.AreEqual(0, _pageLinksCrawlDisallowedCount);
        }

        [Test]
        public void Crawl_PageLinksCrawlDisallowedSubscriberThrowsExceptions_DoesNotCrash()
        {
            _fakeHttpRequester.Setup(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(new CrawledPage(_rootUri));
            _fakeHyperLinkParser.Setup(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>())).Returns(new List<Uri>());
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = false, Reason = "aaa" });

            FifoScheduler _dummyScheduler = new FifoScheduler();
            ThreadManager _dummyThreadManager = new ThreadManager(1);
            _unitUnderTest = new WebCrawler(_dummyThreadManager, _dummyScheduler, _fakeHttpRequester.Object, _fakeHyperLinkParser.Object, _fakeCrawlDecisionMaker.Object, _fakeDomainRateLimiter.Object, new CrawlConfiguration());

            int _pageCrawlStartingCount = 0;
            int _pageCrawlCompletedCount = 0;
            int _pageCrawlDisallowedCount = 0;
            int _pageLinksCrawlDisallowedCount = 0;
            _unitUnderTest.PageCrawlCompleted += (s, e) => ++_pageCrawlCompletedCount;
            _unitUnderTest.PageCrawlStarting += (s, e) => ++_pageCrawlStartingCount;
            _unitUnderTest.PageCrawlDisallowed += (s, e) => ++_pageCrawlDisallowedCount;
            _unitUnderTest.PageLinksCrawlDisallowed += (s, e) => ++_pageLinksCrawlDisallowedCount;
            _unitUnderTest.PageLinksCrawlDisallowed += new EventHandler<PageLinksCrawlDisallowedArgs>(ThrowExceptionWhen_PageLinksCrawlDisallowed);

            _unitUnderTest.Crawl(_rootUri);
            System.Threading.Thread.Sleep(2000);//sleep since the events are async and may not complete, set to 2 seconds since this test was mysteriously failing only when run with code coverage

            _fakeHttpRequester.Verify(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>()), Times.Once());
            _fakeHyperLinkParser.Verify(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>()), Times.Never());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>()), Times.Once());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>()), Times.Once());

            Assert.AreEqual(1, _pageCrawlStartingCount);
            Assert.AreEqual(1, _pageCrawlCompletedCount);
            Assert.AreEqual(0, _pageCrawlDisallowedCount);
            Assert.AreEqual(1, _pageLinksCrawlDisallowedCount);
        }


        [Test]
        public void Crawl_PageCrawlStartingEvent_IsSynchronous()
        {
            int elapsedTimeForLongJob = 1000;

            _fakeHttpRequester.Setup(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(new CrawledPage(_rootUri));
            _fakeHyperLinkParser.Setup(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>())).Returns(new List<Uri>());
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = false, Reason = "aaaa" });

            _unitUnderTest.PageCrawlStarting += new EventHandler<PageCrawlStartingArgs>((sender, args) => System.Threading.Thread.Sleep(elapsedTimeForLongJob));

            Stopwatch timer = Stopwatch.StartNew();
            _unitUnderTest.Crawl(_rootUri);
            timer.Stop();

            Assert.IsTrue(timer.ElapsedMilliseconds > 800);
        }

        [Test]
        public void Crawl_PageCrawlCompletedEvent_IsSynchronous()
        {
            int elapsedTimeForLongJob = 1000;

            _fakeHttpRequester.Setup(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(new CrawledPage(_rootUri));
            _fakeHyperLinkParser.Setup(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>())).Returns(new List<Uri>());
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = false, Reason = "aaaa" });

            _unitUnderTest.PageCrawlCompleted += new EventHandler<PageCrawlCompletedArgs>((sender, args) => System.Threading.Thread.Sleep(elapsedTimeForLongJob));

            Stopwatch timer = Stopwatch.StartNew();
            _unitUnderTest.Crawl(_rootUri);
            timer.Stop();

            Assert.IsTrue(timer.ElapsedMilliseconds > 800);
        }

        [Test]
        public void Crawl_PageCrawlDisallowedEvent_IsSynchronous()
        {
            int elapsedTimeForLongJob = 1000;

            _fakeHttpRequester.Setup(f => f.MakeRequest(It.IsAny<Uri>())).Returns(new CrawledPage(_rootUri));
            _fakeHyperLinkParser.Setup(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>())).Returns(new List<Uri>());
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = false, Reason = "aaa" });

            _unitUnderTest.PageCrawlDisallowed += new EventHandler<PageCrawlDisallowedArgs>((sender, args) => System.Threading.Thread.Sleep(elapsedTimeForLongJob));

            Stopwatch timer = Stopwatch.StartNew();
            _unitUnderTest.Crawl(_rootUri);
            timer.Stop();

            Assert.IsTrue(timer.ElapsedMilliseconds > 800);
        }

        [Test]
        public void Crawl_PageLinksCrawlDisallowedEvent_IsSynchronous()
        {
            int elapsedTimeForLongJob = 1000;

            _fakeHttpRequester.Setup(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(new CrawledPage(_rootUri));
            _fakeHyperLinkParser.Setup(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>())).Returns(new List<Uri>());
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = false, Reason = "aaa" });

            _unitUnderTest.PageLinksCrawlDisallowed += new EventHandler<PageLinksCrawlDisallowedArgs>((sender, args) => System.Threading.Thread.Sleep(elapsedTimeForLongJob));

            Stopwatch timer = Stopwatch.StartNew();
            _unitUnderTest.Crawl(_rootUri);
            timer.Stop();

            Assert.IsTrue(timer.ElapsedMilliseconds > 800);
        }

        #endregion

        #region Async Event Tests

        [Test]
        public void Crawl_CrawlDecisionMakerMethodsReturnTrue_PageCrawlStartingAndCompletedAsyncEventsFires()
        {
            _fakeHttpRequester.Setup(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(new CrawledPage(_rootUri));
            _fakeHyperLinkParser.Setup(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>())).Returns(new List<Uri>());
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });

            int _pageCrawlStartingCount = 0;
            int _pageCrawlCompletedCount = 0;
            int _pageCrawlDisallowedCount = 0;
            int _pageLinksCrawlDisallowedCount = 0;
            _unitUnderTest.PageCrawlCompletedAsync += (s, e) => ++_pageCrawlCompletedCount;
            _unitUnderTest.PageCrawlStartingAsync += (s, e) => ++_pageCrawlStartingCount;
            _unitUnderTest.PageCrawlDisallowedAsync += (s, e) => ++_pageCrawlDisallowedCount;
            _unitUnderTest.PageLinksCrawlDisallowedAsync += (s, e) => ++_pageLinksCrawlDisallowedCount;

            _unitUnderTest.Crawl(_rootUri);
            System.Threading.Thread.Sleep(100);//sleep since the events are async and may not complete before returning

            _fakeHttpRequester.Verify(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>()), Times.Once());
            _fakeHyperLinkParser.Verify(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>()), Times.Once());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>()), Times.Once());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>()), Times.Once());
            _fakeDomainRateLimiter.Verify(f => f.RateLimit(It.IsAny<Uri>()), Times.Never());

            Assert.AreEqual(1, _pageCrawlStartingCount);
            Assert.AreEqual(1, _pageCrawlCompletedCount);
            Assert.AreEqual(0, _pageCrawlDisallowedCount);
            Assert.AreEqual(0, _pageLinksCrawlDisallowedCount);
        }

        [Test]
        public void Crawl_CrawlDecisionMakerShouldCrawlLinksMethodReturnsFalse_PageLinksCrawlDisallowedAsyncEventFires()
        {
            _fakeHttpRequester.Setup(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(new CrawledPage(_rootUri));
            _fakeHyperLinkParser.Setup(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>())).Returns(new List<Uri>());
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = false, Reason = "aaa" });

            int _pageCrawlStartingCount = 0;
            int _pageCrawlCompletedCount = 0;
            int _pageCrawlDisallowedCount = 0;
            int _pageLinksCrawlDisallowedCount = 0;
            _unitUnderTest.PageCrawlCompletedAsync += (s, e) => ++_pageCrawlCompletedCount;
            _unitUnderTest.PageCrawlStartingAsync += (s, e) => ++_pageCrawlStartingCount;
            _unitUnderTest.PageCrawlDisallowedAsync += (s, e) => ++_pageCrawlDisallowedCount;
            _unitUnderTest.PageLinksCrawlDisallowedAsync += (s, e) => ++_pageLinksCrawlDisallowedCount;

            _unitUnderTest.Crawl(_rootUri);
            System.Threading.Thread.Sleep(100);//sleep since the events are async and may not complete before returning

            _fakeHttpRequester.Verify(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>()), Times.Once());
            _fakeHyperLinkParser.Verify(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>()), Times.Never());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>()), Times.Once());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>()), Times.Once());
            _fakeDomainRateLimiter.Verify(f => f.RateLimit(It.IsAny<Uri>()), Times.Never());

            Assert.AreEqual(1, _pageCrawlStartingCount);
            Assert.AreEqual(1, _pageCrawlCompletedCount);
            Assert.AreEqual(0, _pageCrawlDisallowedCount);
            Assert.AreEqual(1, _pageLinksCrawlDisallowedCount);
        }

        [Test]
        public void Crawl_CrawlDecisionMakerShouldCrawlMethodReturnsFalse_PageCrawlDisallowedAsyncEventFires()
        {
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = false, Reason = "aaa" });

            int _pageCrawlStartingCount = 0;
            int _pageCrawlCompletedCount = 0;
            int _pageCrawlDisallowedCount = 0;
            int _pageLinksCrawlDisallowedCount = 0;
            _unitUnderTest.PageCrawlCompletedAsync += (s, e) => ++_pageCrawlCompletedCount;
            _unitUnderTest.PageCrawlStartingAsync += (s, e) => ++_pageCrawlStartingCount;
            _unitUnderTest.PageCrawlDisallowedAsync += (s, e) => ++_pageCrawlDisallowedCount;
            _unitUnderTest.PageLinksCrawlDisallowedAsync += (s, e) => ++_pageLinksCrawlDisallowedCount;

            _unitUnderTest.Crawl(_rootUri);
            System.Threading.Thread.Sleep(100);//sleep since the events are async and may not complete before returning

            _fakeHttpRequester.Verify(f => f.MakeRequest(It.IsAny<Uri>()), Times.Never());
            _fakeHyperLinkParser.Verify(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>()), Times.Never());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>()), Times.Once());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>()), Times.Never());
            _fakeDomainRateLimiter.Verify(f => f.RateLimit(It.IsAny<Uri>()), Times.Never());

            Assert.AreEqual(0, _pageCrawlStartingCount);
            Assert.AreEqual(0, _pageCrawlCompletedCount);
            Assert.AreEqual(1, _pageCrawlDisallowedCount);
            Assert.AreEqual(0, _pageLinksCrawlDisallowedCount);
        }


        [Test]
        public void Crawl_PageCrawlStartingAndCompletedAsyncEventSubscriberThrowsExceptions_DoesNotCrash()
        {
            _fakeHttpRequester.Setup(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(new CrawledPage(_rootUri));
            _fakeHyperLinkParser.Setup(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>())).Returns(new List<Uri>());
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });

            FifoScheduler _dummyScheduler = new FifoScheduler();
            ThreadManager _dummyThreadManager = new ThreadManager(1);
            _unitUnderTest = new WebCrawler(_dummyThreadManager, _dummyScheduler, _fakeHttpRequester.Object, _fakeHyperLinkParser.Object, _fakeCrawlDecisionMaker.Object, _fakeDomainRateLimiter.Object, new CrawlConfiguration());

            int _pageCrawlStartingCount = 0;
            int _pageCrawlCompletedCount = 0;
            int _pageCrawlDisallowedCount = 0;
            int _pageLinksCrawlDisallowedCount = 0;
            _unitUnderTest.PageCrawlCompletedAsync += (s, e) => ++_pageCrawlCompletedCount;
            _unitUnderTest.PageCrawlStartingAsync += (s, e) => ++_pageCrawlStartingCount;
            _unitUnderTest.PageCrawlStartingAsync += new EventHandler<PageCrawlStartingArgs>(ThrowExceptionWhen_PageCrawlStarting);
            _unitUnderTest.PageCrawlCompletedAsync += new EventHandler<PageCrawlCompletedArgs>(ThrowExceptionWhen_PageCrawlCompleted);
            _unitUnderTest.PageCrawlDisallowedAsync += (s, e) => ++_pageCrawlDisallowedCount;
            _unitUnderTest.PageLinksCrawlDisallowedAsync += (s, e) => ++_pageLinksCrawlDisallowedCount;

            _unitUnderTest.Crawl(_rootUri);
            System.Threading.Thread.Sleep(1000);//sleep since the events are async and may not complete

            _fakeHttpRequester.Verify(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>()), Times.Once());
            _fakeHyperLinkParser.Verify(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>()), Times.Once());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>()), Times.Once());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>()), Times.Once());
            _fakeDomainRateLimiter.Verify(f => f.RateLimit(It.IsAny<Uri>()), Times.Never());

            Assert.AreEqual(1, _pageCrawlStartingCount);
            Assert.AreEqual(1, _pageCrawlCompletedCount);
            Assert.AreEqual(0, _pageCrawlDisallowedCount);
            Assert.AreEqual(0, _pageLinksCrawlDisallowedCount);
        }

        [Test]
        public void Crawl_PageCrawlDisallowedAsyncSubscriberThrowsExceptions_DoesNotCrash()
        {
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = false, Reason = "aaa" });

            int _pageCrawlStartingCount = 0;
            int _pageCrawlCompletedCount = 0;
            int _pageCrawlDisallowedCount = 0;
            int _pageLinksCrawlDisallowedCount = 0;
            _unitUnderTest.PageCrawlCompletedAsync += (s, e) => ++_pageCrawlCompletedCount;
            _unitUnderTest.PageCrawlStartingAsync += (s, e) => ++_pageCrawlStartingCount;
            _unitUnderTest.PageCrawlDisallowedAsync += (s, e) => ++_pageCrawlDisallowedCount;
            _unitUnderTest.PageLinksCrawlDisallowedAsync += (s, e) => ++_pageLinksCrawlDisallowedCount;
            _unitUnderTest.PageCrawlDisallowedAsync += new EventHandler<PageCrawlDisallowedArgs>(ThrowExceptionWhen_PageCrawlDisallowed);
            _unitUnderTest.PageLinksCrawlDisallowedAsync += new EventHandler<PageLinksCrawlDisallowedArgs>(ThrowExceptionWhen_PageLinksCrawlDisallowed);

            _unitUnderTest.Crawl(_rootUri);
            System.Threading.Thread.Sleep(1000);//sleep since the events are async and may not complete

            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>()), Times.Once());

            Assert.AreEqual(0, _pageCrawlStartingCount);
            Assert.AreEqual(0, _pageCrawlCompletedCount);
            Assert.AreEqual(1, _pageCrawlDisallowedCount);
            Assert.AreEqual(0, _pageLinksCrawlDisallowedCount);
        }

        [Test]
        public void Crawl_PageLinksCrawlDisallowedAsyncSubscriberThrowsExceptions_DoesNotCrash()
        {
            _fakeHttpRequester.Setup(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(new CrawledPage(_rootUri));
            _fakeHyperLinkParser.Setup(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>())).Returns(new List<Uri>());
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = false, Reason = "aaa" });

            FifoScheduler _dummyScheduler = new FifoScheduler();
            ThreadManager _dummyThreadManager = new ThreadManager(1);
            _unitUnderTest = new WebCrawler(_dummyThreadManager, _dummyScheduler, _fakeHttpRequester.Object, _fakeHyperLinkParser.Object, _fakeCrawlDecisionMaker.Object, _fakeDomainRateLimiter.Object, new CrawlConfiguration());

            int _pageCrawlStartingCount = 0;
            int _pageCrawlCompletedCount = 0;
            int _pageCrawlDisallowedCount = 0;
            int _pageLinksCrawlDisallowedCount = 0;
            _unitUnderTest.PageCrawlCompletedAsync += (s, e) => ++_pageCrawlCompletedCount;
            _unitUnderTest.PageCrawlStartingAsync += (s, e) => ++_pageCrawlStartingCount;
            _unitUnderTest.PageCrawlDisallowedAsync += (s, e) => ++_pageCrawlDisallowedCount;
            _unitUnderTest.PageLinksCrawlDisallowedAsync += (s, e) => ++_pageLinksCrawlDisallowedCount;
            //_unitUnderTest.PageLinksCrawlDisallowed += new EventHandler<PageLinksCrawlDisallowedArgs>(ThrowExceptionWhen_PageLinksCrawlDisallowed);

            _unitUnderTest.Crawl(_rootUri);
            System.Threading.Thread.Sleep(2000);//sleep since the events are async and may not complete, set to 2 seconds since this test was mysteriously failing only when run with code coverage

            _fakeHttpRequester.Verify(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>()), Times.Once());
            _fakeHyperLinkParser.Verify(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>()), Times.Never());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>()), Times.Once());
            _fakeCrawlDecisionMaker.Verify(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>()), Times.Once());
            _fakeDomainRateLimiter.Verify(f => f.RateLimit(It.IsAny<Uri>()), Times.Never());

            Assert.AreEqual(1, _pageCrawlStartingCount);
            Assert.AreEqual(1, _pageCrawlCompletedCount);
            Assert.AreEqual(0, _pageCrawlDisallowedCount);
            Assert.AreEqual(1, _pageLinksCrawlDisallowedCount);
        }


        [Test]
        public void Crawl_PageCrawlStartingAsyncEvent_IsAsynchronous()
        {
            int elapsedTimeForLongJob = 5000;

            _fakeHttpRequester.Setup(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(new CrawledPage(_rootUri));
            _fakeHyperLinkParser.Setup(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>())).Returns(new List<Uri>());
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = false, Reason = "aaaa" });

            _unitUnderTest.PageCrawlStartingAsync += new EventHandler<PageCrawlStartingArgs>((sender, args) => System.Threading.Thread.Sleep(elapsedTimeForLongJob));

            Stopwatch timer = Stopwatch.StartNew();
            _unitUnderTest.Crawl(_rootUri);
            timer.Stop();

            Assert.IsTrue(timer.ElapsedMilliseconds < elapsedTimeForLongJob);
        }

        [Test]
        public void Crawl_PageCrawlCompletedAsyncEvent_IsAsynchronous()
        {
            int elapsedTimeForLongJob = 5000;

            _fakeHttpRequester.Setup(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(new CrawledPage(_rootUri));
            _fakeHyperLinkParser.Setup(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>())).Returns(new List<Uri>());
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = false, Reason = "aaaa" });

            _unitUnderTest.PageCrawlCompletedAsync += new EventHandler<PageCrawlCompletedArgs>((sender, args) => System.Threading.Thread.Sleep(elapsedTimeForLongJob));

            Stopwatch timer = Stopwatch.StartNew();
            _unitUnderTest.Crawl(_rootUri);
            timer.Stop();

            Assert.IsTrue(timer.ElapsedMilliseconds < elapsedTimeForLongJob);
        }

        [Test]
        public void Crawl_PageCrawlDisallowedAsyncEvent_IsAsynchronous()
        {
            int elapsedTimeForLongJob = 5000;

            _fakeHttpRequester.Setup(f => f.MakeRequest(It.IsAny<Uri>())).Returns(new CrawledPage(_rootUri));
            _fakeHyperLinkParser.Setup(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>())).Returns(new List<Uri>());
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = false, Reason = "aaa" });

            _unitUnderTest.PageCrawlDisallowedAsync += new EventHandler<PageCrawlDisallowedArgs>((sender, args) => System.Threading.Thread.Sleep(elapsedTimeForLongJob));

            Stopwatch timer = Stopwatch.StartNew();
            _unitUnderTest.Crawl(_rootUri);
            timer.Stop();

            Assert.IsTrue(timer.ElapsedMilliseconds < elapsedTimeForLongJob);
        }

        [Test]
        public void Crawl_PageLinksCrawlDisallowedAsyncEvent_IsAsynchronous()
        {
            int elapsedTimeForLongJob = 5000;

            _fakeHttpRequester.Setup(f => f.MakeRequest(It.IsAny<Uri>(), It.IsAny<Func<CrawledPage, CrawlDecision>>())).Returns(new CrawledPage(_rootUri));
            _fakeHyperLinkParser.Setup(f => f.GetLinks(It.IsAny<Uri>(), It.IsAny<string>())).Returns(new List<Uri>());
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = true });
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPageLinks(It.IsAny<CrawledPage>(), It.IsAny<CrawlContext>())).Returns(new CrawlDecision { Allow = false, Reason = "aaa" });

            _unitUnderTest.PageLinksCrawlDisallowedAsync += new EventHandler<PageLinksCrawlDisallowedArgs>((sender, args) => System.Threading.Thread.Sleep(elapsedTimeForLongJob));

            Stopwatch timer = Stopwatch.StartNew();
            _unitUnderTest.Crawl(_rootUri);
            timer.Stop();

            Assert.IsTrue(timer.ElapsedMilliseconds < elapsedTimeForLongJob);
        }

        #endregion

        [Test]
        [ExpectedException(typeof(Exception))]
        public void Crawl_FatalExceptionOccurrs()
        {
            Exception fakeException = new Exception("oh no");
            _fakeCrawlDecisionMaker.Setup(f => f.ShouldCrawlPage(It.IsAny<PageToCrawl>(), It.IsAny<CrawlContext>())).Throws(fakeException);

            _unitUnderTest.Crawl(_rootUri);
        }

        private void ThrowExceptionWhen_PageCrawlStarting(object sender, PageCrawlStartingArgs e)
        {
            throw new Exception("no!!!");
        }

        private void ThrowExceptionWhen_PageCrawlCompleted(object sender, PageCrawlCompletedArgs e)
        {
            throw new Exception("Oh No!");
        }

        private void ThrowExceptionWhen_PageCrawlDisallowed(object sender, PageCrawlDisallowedArgs e)
        {
            throw new Exception("no!!!");
        }

        private void ThrowExceptionWhen_PageLinksCrawlDisallowed(object sender, PageLinksCrawlDisallowedArgs e)
        {
            throw new Exception("Oh No!");
        }
    }
}