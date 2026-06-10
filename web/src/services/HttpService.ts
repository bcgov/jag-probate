import axios, { type AxiosInstance, type AxiosResponse } from 'axios';

/**
 * Centralized HTTP service with interceptors for auth error handling.
 * Follows the same pattern as jasper's HttpService.
 */
class HttpService {
  private axiosInstance: AxiosInstance;

  constructor(baseURL: string) {
    this.axiosInstance = axios.create({ baseURL });

    this.axiosInstance.interceptors.response.use(
      (response) => response,
      (error) => this.handleError(error)
    );
  }

  private handleError(error: any) {
    // Let the caller (e.g. auth guard) handle 401s with proper redirect logic.
    // This ensures the correct destination URL is used as returnUrl.
    return Promise.reject(error);
  }

  async get<T>(url: string): Promise<T> {
    const response: AxiosResponse<T> = await this.axiosInstance.get(url);
    return response.data;
  }

  async post<T>(url: string, data?: any): Promise<T> {
    const response: AxiosResponse<T> = await this.axiosInstance.post(url, data);
    return response.data;
  }

  async postBlob(url: string, data?: any): Promise<Blob> {
    const response = await this.axiosInstance.post(url, data, { responseType: 'blob' });
    return response.data;
  }

  async put<T>(url: string, data?: any): Promise<T> {
    const response: AxiosResponse<T> = await this.axiosInstance.put(url, data);
    return response.data;
  }

  async delete<T>(url: string): Promise<T> {
    const response: AxiosResponse<T> = await this.axiosInstance.delete(url);
    return response.data;
  }
}

export default HttpService;
