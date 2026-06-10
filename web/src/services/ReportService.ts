import type { GenerateReportRequest, GenerateReportResponse } from '@/types';
import HttpService from './HttpService';

class ReportService {
  private httpService: HttpService;

  constructor(httpService: HttpService) {
    this.httpService = httpService;
  }

  /**
   * Generates a PDF from the provided submission data and template key.
   * Streams the PDF from the server and returns a local blob URL usable as an iframe src or download link.
   * Caller is responsible for revoking the URL when done: URL.revokeObjectURL(url)
   */
  async generateReport(
    request: GenerateReportRequest
  ): Promise<GenerateReportResponse> {
    const blob = await this.httpService.postBlob(
      'api/Report/generate-from-submission',
      request
    );
    const url = URL.createObjectURL(blob);
    return { url, fileName: `${request.templateKey}.pdf` };
  }
}

export default ReportService;
