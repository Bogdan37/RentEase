using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using RentEase.Models;

namespace RentEase.Services;

public interface IPropertySearchService
{
    void IndexProperty(Property property);
    void IndexProperties(IEnumerable<Property> properties);
    void RemoveProperty(Guid id);
    IEnumerable<Guid> Search(string query, int maxResults = 20);
}

public class PropertySearchService : IPropertySearchService
{
    private const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;
    private readonly RAMDirectory _directory = new();
    private readonly StandardAnalyzer _analyzer = new(AppLuceneVersion);

    public void IndexProperties(IEnumerable<Property> properties)
    {
        using var writer = new IndexWriter(_directory, new IndexWriterConfig(AppLuceneVersion, _analyzer));
        writer.DeleteAll(); 
        
        foreach (var property in properties)
        {
            var doc = CreateDocument(property);
            writer.AddDocument(doc);
        }
        writer.Commit();
    }

    public void IndexProperty(Property property)
    {
        using var writer = new IndexWriter(_directory, new IndexWriterConfig(AppLuceneVersion, _analyzer));
        var doc = CreateDocument(property);
        writer.UpdateDocument(new Term("Id", property.Id.ToString()), doc);
        writer.Commit();
    }

    public void RemoveProperty(Guid id)
    {
        using var writer = new IndexWriter(_directory, new IndexWriterConfig(AppLuceneVersion, _analyzer));
        writer.DeleteDocuments(new Term("Id", id.ToString()));
        writer.Commit();
    }

    public IEnumerable<Guid> Search(string query, int maxResults = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Enumerable.Empty<Guid>();

        if (!DirectoryReader.IndexExists(_directory))
            return Enumerable.Empty<Guid>();

        using var reader = DirectoryReader.Open(_directory);
        var searcher = new IndexSearcher(reader);

        var fields = new[] { "Title", "Description", "City", "Address" };
        var parser = new MultiFieldQueryParser(AppLuceneVersion, fields, _analyzer)
        {
            DefaultOperator = Operator.OR
        };

        var sanitizedQuery = query.Trim();
        Query parsedQuery;
        try
        {
            parsedQuery = parser.Parse(sanitizedQuery);
        }
        catch (ParseException)
        {
            parsedQuery = parser.Parse(QueryParserBase.Escape(sanitizedQuery) + "*");
        }

        var topDocs = searcher.Search(parsedQuery, maxResults);
        var resultIds = new List<Guid>();

        foreach (var hit in topDocs.ScoreDocs)
        {
            var foundDoc = searcher.Doc(hit.Doc);
            if (Guid.TryParse(foundDoc.Get("Id"), out var id))
            {
                resultIds.Add(id);
            }
        }

        return resultIds;
    }

    private static Document CreateDocument(Property property)
    {
        return new Document
        {
            new StringField("Id", property.Id.ToString(), Field.Store.YES),
            new TextField("Title", property.Title ?? string.Empty, Field.Store.NO),
            new TextField("Description", property.Description ?? string.Empty, Field.Store.NO),
            new TextField("City", property.City ?? string.Empty, Field.Store.NO),
            new TextField("Address", property.Address ?? string.Empty, Field.Store.NO)
        };
    }
}