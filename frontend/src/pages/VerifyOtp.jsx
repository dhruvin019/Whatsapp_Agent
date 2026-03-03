import { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import axiosInstance from '../api/axiosInstance';
import { ShieldCheck, Loader2 } from 'lucide-react';

export default function VerifyOtp() {
    const location = useLocation();
    const navigate = useNavigate();
    const phoneNumber = location.state?.phoneNumber;

    const [otp, setOtp] = useState('');
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState('');

    // Protect route if accessed directly without phone number
    if (!phoneNumber) {
        navigate('/');
        return null;
    }

    const handleVerify = async (e) => {
        e.preventDefault();
        if (!otp || otp.length < 6) {
            setError('Please enter a valid 6-digit OTP');
            return;
        }

        setIsLoading(true);
        setError('');

        try {
            const response = await axiosInstance.post('/auth/verify-otp', {
                whatsAppNumber: phoneNumber,
                otp: otp
            });

            if (response.data.token) {
                localStorage.setItem('token', response.data.token);
                navigate('/dashboard');
            } else {
                setError('Login failed. Please try again.');
            }
        } catch (err) {
            // Show actual error — do NOT fallback to a fake token
            setError(err.response?.data?.message || 'Invalid OTP. Please try again.');
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
                    background: 'rgba(16, 185, 129, 0.1)',
                    borderRadius: '50%',
                    marginBottom: '1.5rem'
                }}>
                    <ShieldCheck size={32} color="var(--success)" />
                </div>

                <h1 style={{ fontSize: '1.5rem', marginBottom: '0.5rem' }}>Verify OTP</h1>
                <p style={{ color: 'var(--text-secondary)', marginBottom: '2rem', fontSize: '0.9rem' }}>
                    We've sent a 6-digit code to <strong>{phoneNumber}</strong>
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

                <form onSubmit={handleVerify} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                    <div style={{ textAlign: 'left' }}>
                        <label style={{ display: 'block', marginBottom: '0.5rem', fontSize: '0.875rem', color: 'var(--text-secondary)' }}>
                            6-Digit Code
                        </label>
                        <input
                            type="text"
                            maxLength={6}
                            className="input-field"
                            placeholder="123456"
                            value={otp}
                            onChange={(e) => setOtp(e.target.value.replace(/\D/g, ''))} // only allow numbers
                            disabled={isLoading}
                            style={{ textAlign: 'center', letterSpacing: '0.5rem', fontSize: '1.25rem' }}
                        />
                    </div>

                    <button type="submit" className="btn-primary" disabled={isLoading} style={{ marginTop: '0.5rem' }}>
                        {isLoading ? <Loader2 className="animate-spin" size={20} /> : 'Verify & Login'}
                    </button>

                    <button
                        type="button"
                        onClick={() => navigate('/')}
                        style={{
                            background: 'none',
                            border: 'none',
                            color: 'var(--text-secondary)',
                            fontSize: '0.875rem',
                            cursor: 'pointer',
                            marginTop: '1rem',
                            textDecoration: 'underline'
                        }}
                    >
                        Change Phone Number
                    </button>
                </form>
            </div>
        </div>
    );
}
