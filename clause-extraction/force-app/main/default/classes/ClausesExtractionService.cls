public class ClausesExtractionService {
	public static void getClauses(String url) {
        try {
            herokuapplink.ClausesExtractionAPI_ExtractRequest extractRequest = new herokuapplink.ClausesExtractionAPI_ExtractRequest();
            extractRequest.url = url;

            herokuapplink.ClausesExtractionAPI.ExtractFiles_Request request = new herokuapplink.ClausesExtractionAPI.ExtractFiles_Request();
            request.body = extractRequest;

            herokuapplink.ClausesExtractionAPI clausesExtractionAPI = new herokuapplink.ClausesExtractionAPI();
            herokuapplink.ClausesExtractionAPI.ExtractFiles_Response response = clausesExtractionAPI.ExtractFiles(request);
            herokuapplink.ClausesExtractionAPI_ExtractResponse extractResponse = response.Code200;
            System.debug(JSON.serializePretty(extractResponse));
        }
        catch (herokuapplink.ClausesExtractionAPI.ExtractFiles_ResponseException ex) {
            String message = ex.getMessage();
            if(ex.Code500 != null) message = ex.Code500.error;
            else if(ex.Code400 != null) message = ex.Code400.error;
            else if(ex.Code401 != null) message = ex.Code401.error;
            else if(ex.Code504 != null) message = ex.Code504.error;

            System.debug(message + '\r\n' + ex.getStackTraceString());
            throw new ClausesExtractionException(message);
        }
    }

    public static void getClausesAsync(String url) {
        try {
            herokuapplink.ClausesExtractionAPI_ExtractRequest extractRequest = new herokuapplink.ClausesExtractionAPI_ExtractRequest();
            extractRequest.url = url;

            herokuapplink.ClausesExtractionAPI.ExtractFilesAsync_Request request = new herokuapplink.ClausesExtractionAPI.ExtractFilesAsync_Request();
            request.body = extractRequest;

            herokuapplink.ClausesExtractionAPI clausesExtractionAPI = new herokuapplink.ClausesExtractionAPI();
            herokuapplink.ClausesExtractionAPI.ExtractFilesAsync_Response response = clausesExtractionAPI.ExtractFilesAsync(request);
            herokuapplink.ClausesExtractionAPI_ExtractJobResponse extractResponse = response.Code202;
            System.debug(JSON.serializePretty(extractResponse));
        }
        catch (herokuapplink.ClausesExtractionAPI.ExtractFilesAsync_ResponseException ex) {
            String message = ex.getMessage();
            if(ex.Code500 != null) message = ex.Code500.error;
            else if(ex.Code400 != null) message = ex.Code400.error;
            else if(ex.Code401 != null) message = ex.Code401.error;

            System.debug(message + '\r\n' + ex.getStackTraceString());
            throw new ClausesExtractionException(message);
        }
    }

    public class ClausesExtractionException extends Exception {}
}