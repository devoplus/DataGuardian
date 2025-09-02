using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Devoplus.DataGuardian.Ner;

public sealed class MicrosoftMlTokenizerAdapter
{
    private readonly object? _tokenizer;
    private readonly MethodInfo? _encodeMethod;
    private readonly PropertyInfo? _idsProp;
    private readonly PropertyInfo? _tokensProp;
    private readonly bool _ok;

    public MicrosoftMlTokenizerAdapter(string tokenizerJsonPath)
    {
        try
        {
            var tokType = Type.GetType("Microsoft.ML.Tokenizers.Tokenizer, Microsoft.ML.Tokenizers");
            if (tokType is null)
                return;

            object? tokenizer = null;

            // Path 1: TokenizerModel.FromFile + new Tokenizer(model)
            var modelType = Type.GetType("Microsoft.ML.Tokenizers.TokenizerModel, Microsoft.ML.Tokenizers");
            if (modelType is not null)
            {
                var fromFile = modelType.GetMethod("FromFile", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(string) });
                if (fromFile is not null)
                {
                    var model = fromFile.Invoke(null, new object[] { tokenizerJsonPath });
                    var ctor = tokType.GetConstructor(new[] { modelType });
                    if (ctor is not null)
                        tokenizer = ctor.Invoke(new[] { model });
                }
            }

            // Path 2: Tokenizer.FromFile("tokenizer.json")
            if (tokenizer is null)
            {
                var fromFileTok = tokType.GetMethod("FromFile", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(string) });
                if (fromFileTok is not null)
                {
                    tokenizer = fromFileTok.Invoke(null, new object[] { tokenizerJsonPath });
                }
            }

            // Path 3: Tokenizer.FromJson(File.ReadAllText(...))
            if (tokenizer is null)
            {
                var fromJson = tokType.GetMethod("FromJson", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(string) });
                if (fromJson is not null)
                {
                    var json = File.ReadAllText(tokenizerJsonPath);
                    tokenizer = fromJson.Invoke(null, new object[] { json });
                }
            }

            if (tokenizer is not null)
            {
                _tokenizer = tokenizer;
                _encodeMethod = tokType.GetMethod("Encode", new[] { typeof(string) });
                if (_encodeMethod is null)
                    return;

                var encType = _encodeMethod.ReturnType;
                _idsProp = encType.GetProperty("Ids");
                _tokensProp = encType.GetProperty("Tokens");

                _ok = _idsProp is not null && _tokensProp is not null;
            }
        }
        catch
        {
            _ok = false;
        }
    }

    public (int[] inputIds, int[] attentionMask, int[] tokenTypeIds, string[] tokens) Encode(string text, int maxLen)
    {
        if (_ok && _tokenizer is not null && _encodeMethod is not null && _idsProp is not null && _tokensProp is not null)
        {
            var encoding = _encodeMethod.Invoke(_tokenizer, new object[] { text })!;
            var idsEnum = (System.Collections.IEnumerable)_idsProp.GetValue(encoding)!;
            var toksEnum = (System.Collections.IEnumerable)_tokensProp.GetValue(encoding)!;

            var ids = idsEnum.Cast<object>().Select(o => Convert.ToInt32(o)).ToList();
            var toks = toksEnum.Cast<object>().Select(o => o?.ToString() ?? string.Empty).ToList();

            int n = Math.Min(maxLen, ids.Count);
            var idsArr = ids.Take(n).ToArray();
            var attn = Enumerable.Repeat(1, n).ToArray();
            var typeIds = new int[n];
            var toksArr = toks.Take(n).ToArray();

            return (idsArr, attn, typeIds, toksArr);
        }

        var pieces = text.Split((char[])Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries);
        int m = Math.Min(maxLen, pieces.Length);
        return (Enumerable.Range(0, m).ToArray(), Enumerable.Repeat(1, m).ToArray(), new int[m], pieces.Take(m).ToArray());
    }
}