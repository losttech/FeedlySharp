﻿using FeedlySharp.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FeedlySharp
{
  internal class FeedlyHttpClient : HttpClient
  {
    public string AccessToken { get; set; }


    public FeedlyHttpClient(Uri baseUri) : base()
    {
      BaseAddress = baseUri;
      //DefaultRequestHeaders.Add("Accept", "application/json");
    }


    public async Task<T> AuthRequest<T>(HttpMethod method, string requestUri, Dictionary<string, string> parameters = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class, new()
    {
      if (String.IsNullOrEmpty(AccessToken))
      {
        throw new FeedlySharpException("This request requires an access token.");
      }
      
      return await Request<T>(method, requestUri, parameters, null, cancellationToken, new Dictionary<string,string>()
      {
        { "Authorization", String.Format("OAuth {0}", AccessToken) }
      });
    }


    public async Task<T> AuthRequest<T>(HttpMethod method, string requestUri, dynamic body = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class, new()
    {
      if (String.IsNullOrEmpty(AccessToken))
      {
        throw new FeedlySharpException("This request requires an access token.");
      }

      return await Request<T>(method, requestUri, null, body, cancellationToken, new Dictionary<string, string>()
      {
        { "Authorization", String.Format("OAuth {0}", AccessToken) }
      });
    }


    public async Task<T> Request<T>(HttpMethod method, string requestUri, Dictionary<string, string> parameters = null, dynamic body = null, CancellationToken cancellationToken = default(CancellationToken), Dictionary<string, string> headers = null) where T : class, new()
    {
      HttpRequestMessage request = new HttpRequestMessage(method, requestUri);
      HttpResponseMessage response = null;
      string responseString = null;

      // content of the request
      if (parameters != null)
      {
        request.Content = new FormUrlEncodedContent(parameters);
      }
      // additional headers
      if (headers != null)
      {
        foreach (KeyValuePair<string, string> header in headers)
        {
          request.Headers.Add(header.Key, header.Value);
        }
      }
      // body
      if (body != null)
      {
        request.Content = new StringContent(JsonConvert.SerializeObject(body));
      }

      // make async request
      try
      {
        response = await SendAsync(request, cancellationToken);

        // validate HTTP response
        ValidateResponse(response);

        // read response
        responseString = await response.Content.ReadAsStringAsync();
      }
      catch (HttpRequestException exc)
      {
        throw new FeedlySharpException(exc.Message, exc);
      }
      catch (FeedlySharpException exc)
      {
        throw exc;
      }
      finally
      {
        request.Dispose();

        if (response != null)
        {
          response.Dispose();
        }
      }

      if (responseString == "[]")
      {
        return new T();
      }
      if ((new string[] { "", "{}" }).Contains(responseString))
      {
        return null;
      }

      return DeserializeJson<T>(responseString);
    }


    /// <summary>
    /// Converts JSON to Pocket objects
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="json">Raw JSON response</param>
    /// <returns></returns>
    /// <exception cref="PocketException">Parse error.</exception>
    private T DeserializeJson<T>(string json) where T : class, new()
    {
      json = json.Replace("[]", "{}");

      // deserialize object
      T parsedResponse = JsonConvert.DeserializeObject<T>(
        json,
        new JsonSerializerSettings
        {
          Error = (object sender, ErrorEventArgs args) =>
          {
            throw new FeedlySharpException(String.Format("Parse error: {0}", args.ErrorContext.Error.Message));
          },
          Converters =
          {
            new BoolConverter(),
            new UnixDateTimeConverter(),
            new TimeSpanConverter(),
            new NullableIntConverter(),
            new UriConverter()
          }
        }
      );

      return parsedResponse;
    }



    /// <summary>
    /// Validates the response.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns></returns>
    /// <exception cref="PocketException">
    /// Error retrieving response
    /// </exception>
    private void ValidateResponse(HttpResponseMessage response)
    {
      // no error found
      if (response.IsSuccessStatusCode)
      {
        return;
      }

      throw new Exception(response.StatusCode.ToString()); // TODO
    }


    /// <summary>
    /// Tries to fetch a header value.
    /// </summary>
    /// <param name="headers">The headers.</param>
    /// <param name="key">The key.</param>
    /// <returns></returns>
    private string TryGetHeaderValue(HttpResponseHeaders headers, string key)
    {
      string result = null;

      if (headers == null || String.IsNullOrEmpty(key))
      {
        return null;
      }

      foreach (var header in headers)
      {
        if (header.Key == key)
        {
          var headerEnumerator = header.Value.GetEnumerator();
          headerEnumerator.MoveNext();

          result = headerEnumerator.Current;
          break;
        }
      }

      return result;
    }
  }
}