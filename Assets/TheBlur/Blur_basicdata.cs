using System.Collections;
using System.Collections.Generic;
using System.Linq;
using blurTest;
using UnityEngine;
using System;

namespace blurTest
{
	public abstract class Blur_basicdata : MonoBehaviour
	{
        public bool Emit = false;
		public BlurParameters BlurData;
        public float roll = 0;
        public int rollspeed = 1; //滚动速度
		protected Transform _t;      
        private Blurdata Bluroperating; //正在操作的blur对象		
		protected bool _emit;		
		private static Dictionary<Material, List<Blurdata>> operatingList; 
		private static List<Mesh> Cleanlist;             		
		private static bool RendererOn = false;       
		private static int BlurCount = 0;    
				       		
		protected virtual void Awake()                    
		{                                          
			BlurCount++;          			
			if(BlurCount == 1)
			{
				operatingList = new Dictionary<Material, List<Blurdata>>();  
				Cleanlist = new List<Mesh>();              
			}                                  
						
			_t = transform;
			_emit = Emit;
			
			if (_emit)
			{
				Bluroperating = new Blurdata(GetMaxNumberOfPoints());
				Bluroperating.IsActiveBlur = true;
				OnStartEmit(); //一旦创建一个新轨迹点，“已经移动距离”就清零。	
			}
		}
		
		protected virtual void OnDestroy()         
		{
			BlurCount--;
            Debug.Log("ondestroy");
			if (BlurCount == 0)
			{
				if (Cleanlist != null && Cleanlist.Count > 0)
				{
					foreach (Mesh mesh in Cleanlist)
					{
                        if (Application.isEditor)
                        {
							DestroyImmediate(mesh, true);
                        }else{
							Destroy(mesh);
                        }
					}
				}				
				Cleanlist = null;
				operatingList.Clear();               //上面是清理网格，下面是清理内存。。。应该是这样
				operatingList = null;
			}
			
			if (Bluroperating != null)
			{
				Bluroperating.Dispose();
				Bluroperating = null;
			}
		}
		
		protected virtual void Start()
		{
			
		}
        				
		protected virtual void LateUpdate()
		{
			if(RendererOn) 
				return;			
			RendererOn = true;
			
			foreach (KeyValuePair<Material, List<Blurdata>> keyValuePair in operatingList)   //合并网格。
			{
				CombineInstance[] combineInstances = new CombineInstance[keyValuePair.Value.Count];
				
				for (int i = 0; i < keyValuePair.Value.Count; i++)
				{
					combineInstances[i] = new CombineInstance
					{
						mesh = keyValuePair.Value[i].Mesh,
						subMeshIndex = 0,
						transform = Matrix4x4.identity //底下这两个数据不了解就罢了。。。但子网格到底是什么呢。。。很可能我们的研究能用的上。顺便一说我们现在所看见的是个循环，但其实我们的代码这个循环每次也就跑一次而已？
					};
				}
				
				Mesh combinedMesh = new Mesh();
				combinedMesh.CombineMeshes(combineInstances, true, false);
				Cleanlist.Add(combinedMesh);  				
				DrawMesh(combinedMesh, keyValuePair.Key);				
				keyValuePair.Value.Clear();
			}
		}
		
		protected virtual void Update() //通过分析我们得出。。。RendererOn这个量在跑第一轮update的时候没有变化，即保持了false
		{
			for(int a = 0; a < rollspeed; a++)
			{
                roll -= 0.001f;
            }
			
			if (RendererOn)
			{
				RendererOn = false;				
                if (Cleanlist.Count > 0)   //这个部分在析构函数里也有。。。原因是啥呢？有可能最后一轮update函数运行完了，也存在一些网格没有被消除殆尽  //并且通过分析顺序，我们发现机理是：跑第一圈updatelateupdate的时候，没必要研究怎么去消除该消失的网格，但第二圈开始这个判断工作开始不断执行
				{                
					foreach (Mesh mesh in Cleanlist)//那么我们队toClean里的内容是怎么装进去的太感兴趣了。。。
					{
                        if (Application.isEditor)
                        {
							DestroyImmediate(mesh, true);
                        }
						else
							Destroy(mesh);
					}
				}				
				Cleanlist.Clear();   //上面是清理网格，下面是清理内存。。。应该是这样				
			}
			
			if (operatingList.ContainsKey(BlurData.BlurMaterial) == false) //注意看每生成一个新的blur，新的渲染器数据就生成一次。而每一条blur只对应一个material
			{
				operatingList.Add(BlurData.BlurMaterial, new List<Blurdata>());
			}
						          			
			if (Bluroperating != null) //这个量最初是在awake里面动的，就是生成了我们正执行的blur 所以跑第一轮就开始运行了
			{
				UpdatePoints(Bluroperating, Time.deltaTime);    //来时刻计算正操作blur数组当中点与blur的存在时间       
				UpdateBlur(Bluroperating, Time.deltaTime);       //生成网格数据。而真正画出网格是在lateupdate里
				GenerateMesh(Bluroperating);          //以上三个函数的具体功能我们还要再研究一遍
				operatingList[BlurData.BlurMaterial].Add(Bluroperating);   //好吧今天最大的发现来了。。。似乎是。。每帧都在相同的材质索引下加入一次activeblur，这意味着。。。
			}						
			CheckEmitChange();       //时刻检查emit是不是激活了。。。当然我们目前还没明白这个工作为什么很必要			
		}
				
		protected virtual void OnStopEmit()
		{			
		}
				
		protected virtual void OnStartEmit()
		{
		}
		
		protected virtual void OnTranslate(Vector3 t)
		{
		}
		
		protected abstract int GetMaxNumberOfPoints();//如我们所知这个量是用户事前自己定的。是轨迹点的数量。但环形缓冲里轨迹点数量的编号是没有上限的，这个所谓轨迹点最大数量其实是在间接决定网格点的最大数量
		
	    protected virtual void Reset() //Reset是在用户点击检视面板的Reset按钮或者首次添加该组件时被调用.此函数只在编辑模式下被调用.Reset最常用于在检视面板中给定一个最常用的默认值..			
		{
			if(BlurData == null)
				BlurData = new BlurParameters();			
			BlurData.ColorOverLife = new Gradient();
			BlurData.Lifetime = 1;
			BlurData.SizeOverLife = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));//共有变量在代码里似乎允许不进行初始化，因为我们完全可以在面板里对这些量进行操作
		}
		
		protected virtual void InitialiseNewPoint(BlurdataPoint newPoint)
		{
			//我们的代码没用上这个 这个好像是生成那些雾类轨迹时候要用的，那么我们之后可以研究怎么把这个部分去掉
		}
		
		protected virtual void UpdateBlur(Blurdata blur, float deltaTime)
		{
			
		}
		
		protected void AddPoint(BlurdataPoint newPoint, Vector3 pos)
		{
			if (Bluroperating == null) //就是说如果正执行blur没有的话那还加点干什么
				return;
			
			newPoint.Position = pos;
			newPoint.PointNumber = Bluroperating.Points.Count == 0 ? 0 : Bluroperating.Points[Bluroperating.Points.Count - 1].PointNumber + 1; //注意，网格点与轨迹点在本系统中是两个概念。现在这是在给轨迹点进行编号 而轨迹点的编号是有什么用呢
			//注意看上面这个Bluroperating.Points.Count，这个count，是随着每填入一个点而增加一个。而所谓的”网格点最大数量“，只是我们设定的一个值。注意看我们并没记得轨迹点像网格点那样有个上限数量
			
			newPoint.SetDistanceFromStart(Bluroperating.Points.Count == 0
			                              ? 0   //如果是第一个点，那设置它的移动距离是0
			                              : Bluroperating.Points[Bluroperating.Points.Count - 1].GetDistanceFromStart() + Vector3.Distance(Bluroperating.Points[Bluroperating.Points.Count - 1].Position, pos));
			//每一个点都记录好与起点的距离。这个距离等于上一个点与起点的距离加上目前位置与上一个点的距离  我们好奇的是这个距离后来用以算什么了呢。。。
			//注意看
			//public void SetDistanceFromStart(float distance)
			//{
			//	_distance = distance;
			//}        里面那个_distance正是每次生产一个轨迹点就被清零的量，所以上面那个设置轨迹点已经移动距离的函数，实际并不是在记录每个轨迹点离物体运动起点的距离
			
			
			Bluroperating.Points.Add(newPoint);//加入新点。而加入后具体编号什么的到底怎么搞，这得看那个环形缓冲文件是怎么设计的
		}
		
		private void GenerateMesh(Blurdata blur)
		{
			blur.Mesh.Clear(false);//// 该死不明白这啥意思。按说里面是true的时候这个函数是用来清理内容的
			
			Vector3 camForward = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;  //注意等号右边这是个用户自己规定好了的定值
			
			blur.activePointCount = NumberOfActivePoints(blur);  //此时活动中的blur点数量。这个值是后来用以计算各店颜色与尺寸比例的 这个点指的是。。。轨迹点吧
			
			if (blur.activePointCount < 2) //这个细节理解的模糊点就模糊点吧
				return;
			
			int vertIndex = 0;
			for (int i = 0; i < blur.Points.Count; i++)
			{
				BlurdataPoint p = blur.Points[i];
				float timeAlong = p.TimeActive()/BlurData.Lifetime;   //激活时间与寿命的比例
				
				if(p.TimeActive() > BlurData.Lifetime)
				{
					continue;           //当前点已经过了寿命那就不用再执行下面的了。直接开始下一轮循环。话说这个循环的次序是从小到大，就是说从旧点到新点。所以跳过本轮执行下一轮比较有意义
				}
				
				//每次“摄像头”的Z轴都与当前点的“Z轴”一致。即Z轴覆盖
				
				Vector3 cross = Vector3.zero;
				
				if (i < blur.Points.Count - 1)
				{
					cross =
						Vector3.Cross((blur.Points[i + 1].Position - p.Position).normalized, camForward). normalized;      
				}     
				else
				{
					cross =
						Vector3.Cross((p.Position - blur.Points[i - 1].Position).normalized, camForward)
							.normalized ;        //最后一个点只能研究其与上一个点的连线。
				}
				
				Vector3 maeushro=Vector3.zero;
				float the=1f;
				
				if (i<blur.Points.Count-1 && i>1)
				{
					maeushro=Vector3.Cross((p.Position - blur.Points[i - 1].Position).normalized, (blur.Points[i + 1].Position - p.Position).normalized);//前后端叉积
					the= Vector3.Angle(-(p.Position-blur.Points[i-1].Position).normalized,(blur.Points[i + 1].Position - p.Position).normalized)/180f; 
					
				}   
				else  //如果是最初和最后一个活跃点，那么按以下方式处理。the等于1，就等于是说不对那个网格点与轨迹点之间的距离做处理，我们这么写程序是为了方便，
				{
					the=1f;
					//maeushro=camForward;//SHA DOU WU SUO WEI DE YI SI
				}
				
				Color c =  BlurData.ColorOverLife.Evaluate(timeAlong);
				float s =  BlurData.SizeOverLife.Evaluate(timeAlong);
				
				if( Vector3.Dot(maeushro,camForward)<0)
				{

					blur.verticies[vertIndex] = p.Position + cross * s * BlurData.blursize*the*the;  //*the*the*the*the
					
					float timeAlongx1 = (p.TimeActive()+roll)/BlurData.Lifetime;
					blur.uvs[vertIndex] = new Vector2( timeAlongx1-(float)Math.Truncate(timeAlongx1) , 1);
					
					
					blur.normals[vertIndex] = Vector3.right;     //这个。。。发现我们似乎没有用到。。。发现就是他们的相机Z轴，这个值受是否启用Z轴覆盖的影响。这个应该是所谓的纹理发现吧？我们想看看如果我们不对这个数组进行操作，那会是啥感觉 
					blur.colors[vertIndex] = c;          //这里只是利用那个用户界面的颜色曲线来决定每一个部分的色度
					vertIndex++;
					
					
					//&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
					blur.verticies[vertIndex] = p.Position - cross * s * BlurData.blursize;
					
					
					float timeAlongx2 = (p.TimeActive()+roll)/BlurData.Lifetime;
					blur.uvs[vertIndex] = new Vector2( timeAlongx2-(float)Math.Truncate(timeAlongx2) , 0);
					
					blur.normals[vertIndex] = camForward; // 
					blur.colors[vertIndex] = c;
					
					vertIndex++;   
				}
				
				else if(Vector3.Dot(maeushro,camForward)>0)
				{
					
					blur.verticies[vertIndex] = p.Position + cross * s * BlurData.blursize;  
					
					float timeAlongx1 = (p.TimeActive()+roll)/BlurData.Lifetime;
					blur.uvs[vertIndex] = new Vector2( timeAlongx1-(float)Math.Truncate(timeAlongx1) , 1);
					
					
					blur.normals[vertIndex] = Vector3.right;     //这个。。。发现我们似乎没有用到。。。发现就是他们的相机Z轴，这个值受是否启用Z轴覆盖的影响。这个应该是所谓的纹理发现吧？我们想看看如果我们不对这个数组进行操作，那会是啥感觉 
					blur.colors[vertIndex] = c;          //这里只是利用那个用户界面的颜色曲线来决定每一个部分的色度
					vertIndex++;
					
					
					//&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&

					blur.verticies[vertIndex] = p.Position - cross * s * BlurData.blursize*the; //*the*the*the*the
					
					
					float timeAlongx2 = (p.TimeActive()+roll)/BlurData.Lifetime;
					blur.uvs[vertIndex] = new Vector2( timeAlongx2-(float)Math.Truncate(timeAlongx2) , 0);
					
					blur.normals[vertIndex] = camForward; // 
					blur.colors[vertIndex] = c;
					
					vertIndex++;   
					
				}
				else if(Vector3.Dot(maeushro,camForward)==0)
				{
					
					blur.verticies[vertIndex] = p.Position + cross * s * BlurData.blursize;  
					
					float timeAlongx1 = (p.TimeActive()+roll)/BlurData.Lifetime;
					blur.uvs[vertIndex] = new Vector2( timeAlongx1-(float)Math.Truncate(timeAlongx1) , 1);
					
					
					blur.normals[vertIndex] = camForward;     //这个。。。发现我们似乎没有用到。。。发现就是他们的相机Z轴，这个值受是否启用Z轴覆盖的影响。这个应该是所谓的纹理发现吧？我们想看看如果我们不对这个数组进行操作，那会是啥感觉 
					blur.colors[vertIndex] = c;          //这里只是利用那个用户界面的颜色曲线来决定每一个部分的色度
					vertIndex++;
					
					
					//&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&
					blur.verticies[vertIndex] = p.Position - cross * s * BlurData.blursize;
					
					
					float timeAlongx2 = (p.TimeActive()+roll)/BlurData.Lifetime;
					blur.uvs[vertIndex] = new Vector2( timeAlongx2-(float)Math.Truncate(timeAlongx2) , 0);
					
					blur.normals[vertIndex] = camForward; // 
					blur.colors[vertIndex] = c;
					
					vertIndex++;   
					
				}
				
				
			}
			
			///////////////////////////////////////////////////////////////
			Vector2 finalPosition = blur.verticies[vertIndex-1];//这个位置为什么说是vector2我们还真是没弄清楚
			for(int i = vertIndex; i < blur.verticies.Length; i++)//这里面的verticies。length是事前规定的轨迹点最大数决定的。
			{
				blur.verticies[i] = finalPosition;  //决定最终位置点。。。即是说。。。“暂时没生出的网格点”全都是计做“最后位置”，即现在跑最前面那个位置。注意是网格点
			}
			
			int indIndex = 0;
			for (int pointIndex = 0; pointIndex < 2 * (blur.activePointCount - 1); pointIndex++)
			{
				if(pointIndex%2==0)
				{
					blur.indicies[indIndex] = pointIndex;
					indIndex++;
					blur.indicies[indIndex] = pointIndex + 1;
					indIndex++;
					blur.indicies[indIndex] = pointIndex + 2;
				}
				else
				{
					blur.indicies[indIndex] = pointIndex + 2;
					indIndex++;
					blur.indicies[indIndex] = pointIndex + 1;
					indIndex++;
					blur.indicies[indIndex] = pointIndex;
				}
				
				indIndex++;                //生成三角形。感觉我们接下来的研究应该没必要在这个部分下什么功夫
			}
			
			int finalIndex = blur.indicies[indIndex-1];
			for (int i = indIndex; i < blur.indicies.Length; i++)
			{
				blur.indicies[i] = finalIndex;        // 这是说。。。“暂时没生出的网格点”编号全一致，这样倒数第二个点连在所有未生成点上。。。就是说这一段其实是在针对啥时期？是BLUR刚生产不久还没有拖很长的时候。
			}
			
			blur.Mesh.vertices = blur.verticies;
			blur.Mesh.SetIndices(blur.indicies, MeshTopology.Triangles, 0);
			blur.Mesh.uv = blur.uvs;
			blur.Mesh.normals = blur.normals;
			blur.Mesh.colors = blur.colors;
		}
		
		private void DrawMesh(Mesh blurMesh, Material blurMaterial)
		{
			Graphics.DrawMesh(blurMesh, Matrix4x4.identity, blurMaterial, gameObject.layer);     //绘图函数。
		}
		
		private void UpdatePoints(Blurdata line, float deltaTime)
		{
			for (int i = 0; i < line.Points.Count; i++)
			{
				line.Points[i].Update(deltaTime);      //点的时间更新
			}
		}
		
		[Obsolete("UpdatePoint is deprecated, you should instead override UpdateBlur and loop through the individual points yourself (See Smoke or Smoke Plume scripts for how to do this).", true)]
		protected virtual void UpdatePoint(BlurdataPoint BlurdataPoint, float deltaTime)
		{
		}
		
		private void CheckEmitChange()
		{
			if (_emit != Emit)
			{
				_emit = Emit;
				if (_emit)
				{
					Bluroperating = new Blurdata(GetMaxNumberOfPoints());
					Bluroperating.IsActiveBlur = true;
					
					OnStartEmit();
				}
				else
				{
					OnStopEmit();
					Bluroperating.IsActiveBlur = false;
					Bluroperating = null;
				}
			}
		}
		
		private int NumberOfActivePoints(Blurdata line)
		{
			int count = 0;
			for (int index = 0; index < line.Points.Count; index++)
			{
				if (line.Points[index].TimeActive() < BlurData.Lifetime) count++;
			}
			return count;
		}
		
		/// <summary>
		/// Translates every point in the vector t
		/// </summary>
		public void Translate(Vector3 t)
		{
			if (Bluroperating != null)
			{
				for (int i = 0; i < Bluroperating.Points.Count; i++)
				{
					Bluroperating.Points[i].Position += t;
				}
			}
			
			
			OnTranslate(t);
		}
		
		/// <summary>
		/// Insert a blur into this blur renderer. 
		/// </summary>
		/// <param name="from">The start position of the blur.</param>
		/// <param name="to">The end position of the blur.</param>
		/// <param name="distanceBetweenPoints">Distance between each point on the blur</param>
		
		/// <summary>
		/// Clears all active blurs from the system.
		/// </summary>
		/// <param name="emitState">Desired emit state after clearing</param>
		public void ClearSystem(bool emitState)
		{
			if(Bluroperating != null)
			{
				Bluroperating.Dispose();
				Bluroperating = null;
			}
			
			
			Emit = emitState;
			_emit = !emitState;
			
			CheckEmitChange();
		}
		
		public int NumSegments()
		{
			int num = 0;
			if (Bluroperating != null && NumberOfActivePoints(Bluroperating) != 0)
				num++;
			
			return num;
		}
	}
	
	public class Blurdata : System.IDisposable
	{
		public CircularBuffer<BlurdataPoint> Points;
		public Mesh Mesh;
		
		public Vector3[] verticies;
		public Vector3[] normals;
		public Vector2[] uvs; 
		public Color[] colors; 
		public int[] indicies;
		public int activePointCount;
		
		public bool IsActiveBlur = false;
		
		public Blurdata(int numPoints)
		{
			Mesh = new Mesh();
			Mesh.MarkDynamic();
			
			verticies = new Vector3[2 * numPoints];
			normals = new Vector3[2 * numPoints];
			uvs = new Vector2[2 * numPoints];
			colors = new Color[2 * numPoints];
			indicies = new int[2 * (numPoints) * 3];     //这些乘以2，都是指的轨迹点的两侧所对称着的网格点。
			
			Points = new CircularBuffer<BlurdataPoint>(numPoints);
		}
		
		#region Implementation of IDisposable
		
		public void Dispose()
		{
			if(Mesh != null)
			{
                if(Application.isEditor)
                {
					//UnityEngine.Object.DestroyImmediate(Mesh, true);
                }
                else{
					//UnityEngine.Object.Destroy(Mesh);
                }
			}
			
			Points.Clear();
			Points = null;
		}
		
		#endregion
	}
	
	public class BlurdataPoint  
	{
		//public Vector3 Forward;
		public Vector3 Position;
		public int PointNumber;
		
		private float _timeActive = 0;
		private float _distance;
		
		public virtual void Update(float deltaTime)
		{
			_timeActive += deltaTime;
		}
		
		public float TimeActive()
		{
			return _timeActive;
		}
		
		public void SetTimeActive(float time)
		{
			_timeActive = time;
		}
		
		public void SetDistanceFromStart(float distance)
		{
			_distance = distance;
		}
		
		public float GetDistanceFromStart()
		{
			return _distance;
		}
	}
	
	[System.Serializable]
	public class BlurParameters
	{
		public Material BlurMaterial;
		public float Lifetime = 1;
		public AnimationCurve SizeOverLife = new AnimationCurve();
		public Gradient ColorOverLife;
		public float blursize = 1;
	}
}
