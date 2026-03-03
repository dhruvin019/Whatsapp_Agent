import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import axiosInstance from '../api/axiosInstance';
import { MessageSquare, ArrowRight, Loader2 } from 'lucide-react';

export default function Login() {
  const [phoneNumber, setPhoneNumber] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const navigate = useNavigate();

  const handleRequestOtp = async (e) => {
    e.preventDefault();
    if (!phoneNumber) {
      setError('Please enter a valid WhatsApp number');
      return;
    }

    setIsLoading(true);
    setError('');

    try {
      // Assuming backend runs on localhost:5238 or similar (will configure proxy later if needed)
      // For now, hit the absolute URL or rely on CORS. Assuming default .NET local port: 5078/7057
      // We will refine the base URL in an axios instance later, using a relative path for now 
      // mixed with Vite proxy or full URL if known. Let's use standard localhost:5000 for demo.

      const response = await axiosInstance.post('/auth/request-otp', {
        whatsAppNumber: phoneNumber
      });

      if (response.status === 200) {
        // Pass the phone number to the next screen via React Router state
        navigate('/verify', { state: { phoneNumber } });
      }
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to send OTP. Is the backend running on port 5257?');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="animate-fade-in" style={{
      display: 'flex',
      minHeight: '100vh',
      alignItems: 'center',
      justifyContent: 'center',
      padding: '1rem'
    }}>
      <div className="glass-panel" style={{
        width: '100%',
        maxWidth: '400px',
        padding: '2rem',
        textAlign: 'center'
      }}>

        <div style={{
          display: 'inline-flex',
          padding: '1rem',
          background: 'rgba(59, 130, 246, 0.1)',
          borderRadius: '50%',
          marginBottom: '1.5rem'
        }}>
          <MessageSquare size={32} color="var(--primary)" />
        </div>

        <h1 style={{ fontSize: '1.5rem', marginBottom: '0.5rem' }}>Welcome Back</h1>
        <p style={{ color: 'var(--text-secondary)', marginBottom: '2rem', fontSize: '0.9rem' }}>
          Enter your WhatsApp number to login or register
        </p>

        {error && (
          <div style={{
            padding: '0.75rem',
            background: 'rgba(239, 68, 68, 0.1)',
            border: '1px solid rgba(239, 68, 68, 0.2)',
            color: 'var(--error)',
            borderRadius: '0.5rem',
            marginBottom: '1rem',
            fontSize: '0.875rem'
          }}>
            {error}
          </div>
        )}

        <form onSubmit={handleRequestOtp} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <div style={{ textAlign: 'left' }}>
            <label style={{ display: 'block', marginBottom: '0.5rem', fontSize: '0.875rem', color: 'var(--text-secondary)' }}>
              WhatsApp Number
            </label>
            <input
              type="tel"
              className="input-field"
              placeholder="+1234567890"
              value={phoneNumber}
              onChange={(e) => setPhoneNumber(e.target.value)}
              disabled={isLoading}
            />
          </div>

          <button type="submit" className="btn-primary" disabled={isLoading} style={{ marginTop: '0.5rem' }}>
            {isLoading ? <Loader2 className="animate-spin" size={20} /> : 'Send OTP'}
            {!isLoading && <ArrowRight size={20} />}
          </button>
        </form>
      </div>
    </div>
  );
}
