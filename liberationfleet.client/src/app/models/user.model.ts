export interface User {
  id: number;
  username: string;
  email: string;
}

export interface CreateUserRequest {
  username: string;
  email: string;
  password: string;
  confirmPassword: string;
}

export interface ResetPasswordRequest {
  token: string;
  newPassword: string;
  confirmPassword: string;
}

export interface AuthResult {
  success: boolean;
  message?: string;
  token?: string;
  user?: User;
}

export interface PasswordResetResult {
  success: boolean;
  message: string;
}

export interface ValidateResetTokenResult {
  isValid: boolean;
  message: string;
  email?: string;
}
