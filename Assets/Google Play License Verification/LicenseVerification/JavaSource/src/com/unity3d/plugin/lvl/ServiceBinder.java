package com.unity3d.plugin.lvl;

import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.ServiceConnection;
import android.os.IBinder;
import android.os.RemoteException;

public class ServiceBinder extends android.os.Binder implements ServiceConnection
{
	private final Context mContext;
	public ServiceBinder(Context context)
	{
		mContext = context;
	}

	private Runnable mDone = null;
	private int mNonce;
	public void create(int nonce, Runnable done)
	{
		if (mDone != null)
		{
			destroy();
			_arg0 = -1;
			mDone.run();
		}
		mNonce = nonce;
		mDone = done;
		Intent serviceIntent = new Intent(SERVICE);
		serviceIntent.setPackage("com.android.vending");
		if (mContext.bindService(serviceIntent, this, Context.BIND_AUTO_CREATE))
			return;

		mDone.run();
	}
	private void destroy()
	{
		mContext.unbindService(this);
	}

	private static final String SERVICE = "com.android.vending.licensing.ILicensingService";
	public void onServiceConnected(ComponentName name, IBinder service) {
		android.os.Parcel _data = android.os.Parcel.obtain();
		_data.writeInterfaceToken(SERVICE);
		_data.writeLong(mNonce);
		_data.writeString(mContext.getPackageName());
		_data.writeStrongBinder(this);
		try {
			service.transact(1/*Stub.TRANSACTION_checkLicense*/, _data,	null, IBinder.FLAG_ONEWAY);
		}
		catch (Exception ex)
		{
			ex.printStackTrace();
		}
		finally {
			_data.recycle();
		}
	}

	private static final String LISTENER = "com.android.vending.licensing.ILicenseResultListener";
	public boolean onTransact(int code, android.os.Parcel data,
			android.os.Parcel reply, int flags)
			throws android.os.RemoteException {
		switch (code) {
		case INTERFACE_TRANSACTION: {
			reply.writeString(LISTENER);
			return true;
		}
		case 1/*TRANSACTION_verifyLicense*/: {
			data.enforceInterface(LISTENER);
			_arg0 = data.readInt();
			_arg1 = data.readString();
			_arg2 = data.readString();
			mDone.run();
			destroy();
			return true;
		}
		}
		return super.onTransact(code, data, reply, flags);
	}

	public void onServiceDisconnected(ComponentName name) {
	}

	int _arg0;
	String _arg1;
	String _arg2;
}