import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { from, switchMap, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

function withAuthHeader(req: HttpRequest<unknown>, token: string) {
  return req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const isApiRequest = req.url.startsWith(environment.apiUrl);
  const isAuthRequest = req.url.startsWith(`${environment.apiUrl}/auth/`);

  const token = authService.token();
  const authorizedReq = token && isApiRequest ? withAuthHeader(req, token) : req;

  return next(authorizedReq).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || isAuthRequest) {
        return throwError(() => error);
      }

      return from(authService.refresh()).pipe(
        switchMap(() => {
          const newToken = authService.token();
          const retriedReq = newToken ? withAuthHeader(req, newToken) : req;
          return next(retriedReq);
        }),
        catchError(() => throwError(() => error)),
      );
    }),
  );
};
