#include "..\STDInclude.h"

namespace Ayria 
{
	ClientResourceHandler::~ClientResourceHandler()
	{

	}

	bool ClientResourceHandler::ProcessRequest(CefRefPtr<CefRequest> request, CefRefPtr<CefCallback> callback)
	{
		assert(CefCurrentlyOn(TID_IO));

		std::string url = request->GetURL();
		callback->Continue();
		return false;
	}

	void ClientResourceHandler::GetResponseHeaders(CefRefPtr<CefResponse> response, int64& response_length, CefString& redirectUrl)
	{
		assert(CefCurrentlyOn(TID_IO));

		//response->SetStatus(302);

	// 	CefResponse::HeaderMap map;
	// 	response->GetHeaderMap(map);
	// 
	// 	map.insert(std::make_pair("cache-control", "no-cache, must-revalidate"));
	// 	response->SetHeaderMap(map);

		//response_length = 0;
		redirectUrl = "http://v-multi.com/cef2.html";
		//redirectUrl = "http://google.com";
	}

	void ClientResourceHandler::Cancel()
	{
		assert(CefCurrentlyOn(TID_IO));
	}

	bool ClientResourceHandler::ReadResponse(void* data_out, int bytes_to_read, int& bytes_read, CefRefPtr<CefCallback> callback)
	{
		assert(CefCurrentlyOn(TID_IO));

		bytes_read = 0;
		callback->Continue();
		return false;
	}
}