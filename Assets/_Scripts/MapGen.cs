using UnityEngine;
using System.Collections;

public class MapGen : MonoBehaviour {
	public int seed = 0;
	public Terrain m_ter;
	public Terrain m_err;
	public WaterSimple water;
	// options are from 0.0 (sealevel) to 1.0 (highest peak)
	public float m_sandHeight = 0.1f;
	public float m_rockStartHeight = 0.5f;
	public float m_rockEndHeight = 0.8f;
	public float m_treeline = 0.7f;
	// options are from 0.0 (none) to 1.0 (lots)
	public float m_grass = 0.2f;
	public float m_trees = 0.2f;
	
	//rain
	public float startingVolume = 0.001f;
	
    private float maxHeight;
    private float minHeight;
	
	WaterCell[,] waterSim;
	float[,] waterlevel ;
	
	// Use this for initialization
	void Start () {
		GenerateTerrain();
		
		InitWater();
		TextureTerrain();
		GrassTerrain();
		AddTrees();
	}
	
	// Update is called once per frame
	void Update () {
		RunWater ();
	}

		
	void GenerateTerrain (){
		
        float[,] heights = new float[m_ter.terrainData.heightmapWidth, m_ter.terrainData.heightmapHeight];
        for (int i = 0; i < m_ter.terrainData.heightmapWidth; i++)
        {
            for (int k = 0; k < m_ter.terrainData.heightmapHeight; k++)
            {
				float xCoord = ((float)i / (float)m_ter.terrainData.heightmapWidth);
				float zCoord = ((float)k / (float)m_ter.terrainData.heightmapHeight);
				float offset = (float)seed;
				// form a sort of base
				float noise =Mathf.Sqrt(Mathf.Sin( Mathf.PI*xCoord)*Mathf.Sin( Mathf.PI*zCoord));
				noise *= (Mathf.PerlinNoise(xCoord*4.0f+offset,zCoord*4.0f+offset))/1.0f; // 4 = island sizes features
				//noise*=noise; // peakyer
				//Add features
				noise += (Mathf.PerlinNoise(xCoord*16.0f+offset,zCoord*16.0f+offset)-0.5f)/20.0f; // 16 = football sized features
				noise += (Mathf.PerlinNoise(xCoord*64.0f+offset,zCoord*64.0f+offset)-0.5f)/100.0f; // 64 = room sized features
							
				maxHeight = Mathf.Max(maxHeight,noise);
				minHeight = Mathf.Min(minHeight,noise);
				
				heights[i, k] = noise;

            }
        }
 
        m_ter.terrainData.SetHeights(0, 0, heights);
    }
	
	void TextureTerrain (){
		//float maxHeight = 0.0f;
		float maxRelHeight = 0.0f;
		
		float[, ,] splatmapData = new float[m_ter.terrainData.alphamapWidth, m_ter.terrainData.alphamapHeight, m_ter.terrainData.alphamapLayers];		
		for (int y = 0; y < m_ter.terrainData.alphamapHeight; y++)		{
			for (int x = 0; x < m_ter.terrainData.alphamapWidth; x++){
				// this assumes alpha map size is the same as the height map
				
				// UnityGetHeight returns the height value in the range [0.0f, Terrain Height]
				// GetHeights returns the height values in the range of [0.0f,1.0f]
				// SetHeights expect the height values in the range of [0.0f,1.0f].
				// So when using GetHeight you have to manually divide the value by the 
				// Terrain.activeTerrain.terrainData.size.y which is the configured height ("Terrain Height") of the terrain.
				
				float absHeight = m_ter.terrainData.GetHeight(y,x);// absolute height in gameworld
				float waterHeight = water.transform.position.y;// absolute sea position
				float relHeight = (absHeight-waterHeight)/(maxHeight*m_ter.terrainData.size.y-waterHeight); // 0,1 where 0 is sea and 1 is max height of terrain
						
				float xCoord = ((float)x / (float)m_ter.terrainData.alphamapWidth);
				float zCoord = ((float)y / (float)m_ter.terrainData.alphamapHeight);
				float slope = m_ter.terrainData.GetSteepness(zCoord,xCoord);
				
				maxRelHeight = Mathf.Max(maxRelHeight,relHeight);
				//minHeight = Mathf.Min(minHeight,height);
					
				float noise = 2.0f*(Mathf.PerlinNoise(xCoord*32.0f,zCoord*32.0f)); // 16 = football sized features	
				
				float grass = 1.0f;
				float sand = 0.0f;
				float rock = 0.0f;
				float cliff = 0.0f;
				if (slope > 50.0f){
					cliff=(slope-50.0f)/40.0f;
				}
				
				if (relHeight>m_rockEndHeight) {// very mountinous
					grass = 0.0f;
					sand = 0.0f;
					rock = 1.0f;
				}
				else if (relHeight>m_rockStartHeight) {// starting to get rocky
					float rat = (relHeight-m_rockStartHeight)/(m_rockEndHeight-m_rockStartHeight);
					grass = 1.0f - noise*rat;
					sand = 0.0f;
					rock = rat;
				}
				else if (relHeight>m_sandHeight) {// grassy plains
					grass = 1.0f;
					sand = 0.0f;
					rock = 0.0f;
				}
				else if (relHeight>0.0) {// beach
					float rat = (relHeight)/(m_sandHeight);
					grass = noise*rat;
					sand = 1.0f - rat;
					rock = 0.0f;
				}
				else{ //seabed
					grass = 0.0f;
					sand = 1.0f;
					rock = 0.0f;
				}		
				// normalise
				float sum = grass+sand+rock+cliff;
				
				// assign textures
		        splatmapData[x, y, 0] = grass/sum;
		        splatmapData[x, y, 1] = sand/sum;
		        splatmapData[x, y, 2] = rock/sum;
		        splatmapData[x, y, 3] = cliff/sum;
				
			}
		}
		m_ter.terrainData.SetAlphamaps(0, 0, splatmapData);


	}	
	
	void GrassTerrain (){
		
		
		int[,] shortGrass = m_ter.terrainData.GetDetailLayer(0,0, m_ter.terrainData.detailWidth, m_ter.terrainData.detailHeight, 0);
		int[,] longGrass = m_ter.terrainData.GetDetailLayer(0,0, m_ter.terrainData.detailWidth, m_ter.terrainData.detailHeight, 1);
		
		for (int y = 0; y < m_ter.terrainData.detailHeight; y++)		{
			for (int x = 0; x < m_ter.terrainData.detailWidth; x++){
								
				float xCoord = ((float)x / (float)m_ter.terrainData.detailWidth);
				float zCoord = ((float)y / (float)m_ter.terrainData.detailHeight);
				
				float absHeight = m_ter.terrainData.GetInterpolatedHeight(zCoord,xCoord);// absolute height in gameworld
				float waterHeight = water.transform.position.y;// absolute sea position
				float relHeight = (absHeight-waterHeight)/(maxHeight*m_ter.terrainData.size.y-waterHeight); // 0,1 where 0 is sea and 1 is max height of terrain
				

				// random distribution
				float grassyness = (Mathf.PerlinNoise(xCoord*256.0f,zCoord*256.0f)); 
				
				// punish grass on cliffs
				float slope = m_ter.terrainData.GetSteepness(zCoord,xCoord);
				if (slope>50.0f){
					grassyness*=1.0f-((slope-50.0f)/40.0f);
				}
				

				// punish grass on beach
				if (relHeight<m_sandHeight){
					grassyness*=(relHeight/m_sandHeight);
				}
								
				// remove grass underwater
				if (relHeight<0.0f){
					grassyness = 0.0f;
				}
				
				// todo, have several grass layers and pick one at random
				int numGrass = 0;
				if (grassyness>(1.0f-m_grass)){
				    numGrass = 1;	
				}
				shortGrass[x,y] = numGrass;
				longGrass[x,y] = 0;
			}
		}
		m_ter.terrainData.SetDetailLayer(0, 0,0,shortGrass);
		m_ter.terrainData.SetDetailLayer(0, 0,1,longGrass);
	}
	
	
	
	
	void AddTrees (){
		m_ter.terrainData.treeInstances = new TreeInstance[]{};
  		int grid = 8; // size of 'grid' to scan accross terrain, 1 tree per grid
		for (int y = 0; y < m_ter.terrainData.detailHeight; y+=grid)		{
			for (int x = 0; x < m_ter.terrainData.detailWidth; x+=grid){
				
				float xCoord = ((float)x / (float)m_ter.terrainData.detailWidth);
				float zCoord = ((float)y / (float)m_ter.terrainData.detailHeight);
				
				float absHeight = m_ter.terrainData.GetInterpolatedHeight(zCoord,xCoord);// absolute height in gameworld
				float waterHeight = water.transform.position.y;// absolute sea position
				float relHeight = (absHeight-waterHeight)/(maxHeight*m_ter.terrainData.size.y-waterHeight); // 0,1 where 0 is sea and 1 is max height of terrain
				// 
				float treeness = (Mathf.PerlinNoise(xCoord*64.0f,zCoord*64.0f)); // 
				int type = Random.Range(0, 9);
								
				// punish trees on cliffs
				float slope = m_ter.terrainData.GetSteepness(zCoord,xCoord);
				float slopePunishment = 20.0f;
				if (slope>slopePunishment){
					treeness*=1.0f-((slope-slopePunishment)/(90.0f-slopePunishment));
				}
				
				// beaches
				if (relHeight<m_sandHeight){
					treeness*=(relHeight/m_sandHeight);
				}
				
				// remove trees underwater
				if (relHeight<0.0f){
					treeness = 0.0f;
				}
				
				// treeline	
				float treelineStrictness = 0.1f;
				if (relHeight>m_treeline){
					treeness *= 1.0f - ((relHeight-m_treeline)/treelineStrictness);
				}
				
				if (treeness>(1.0f-m_trees)){
	      			TreeInstance t = new TreeInstance();
					// place tree randomly inside grid
			        Vector2 r = Random.insideUnitCircle * (float) grid / (float) m_ter.terrainData.detailWidth;
			        t.prototypeIndex = type;
					t.color = Color.white;
					t.heightScale = 1.0f;
					t.widthScale = 1.0f;
					t.lightmapColor= Color.white;
			        t.position = new Vector3(zCoord+r.y,0.0f,xCoord+r.x);
			        m_ter.AddTreeInstance(t);
					//Debug.Log (t);
   				}
    		}
  		}
		 m_ter.Flush();
	}
	
	
	
	
	
	void InitWater (){
		//WaterCell[,] water =  new WaterCell[m_ter.terrainData.heightmapWidth,m_ter.terrainData.heightmapHeight];// allocate array
		waterSim =  new WaterCell[m_ter.terrainData.heightmapWidth,m_ter.terrainData.heightmapHeight];// allocate array
		waterlevel = new float[m_ter.terrainData.heightmapWidth, m_ter.terrainData.heightmapHeight];
		
		for (int x = 0; x < m_ter.terrainData.heightmapWidth; x++)
		{
		    for (int y = 0; y < m_ter.terrainData.heightmapHeight; y++)
			{	
				waterSim[x,y] = new WaterCell(); // invoke constructor
				waterSim[x,y].volume = startingVolume;
				waterSim[x,y].newVolume = startingVolume;
				waterSim[x,y].iteration = 0;
				waterlevel[x,y] = 0.0f;
			}
		}
	}
			
	void RunWater (){
		// neighbour information
		int[] nextX = {0,+1,0,-1};
		int[] nextY = {+1,0,-1,0};
		int next = 4;
		
		//float maxFlow = 0.01f;
		float damping = 0.45f;
		
		// iteration information
		//int maxIterations = 64;
		int it = Time.frameCount;
		//for (int it = 0; it<maxIterations; it++){// iteration
			for (int x = 1; x < m_ter.terrainData.heightmapWidth-1; x++)
		    {
		        for (int y = 1; y < m_ter.terrainData.heightmapHeight-1; y++)
				{	  
					float height = m_ter.terrainData.GetHeight(x,y)/m_ter.terrainData.size.y;// 0,1
				
					// update cell					
					if (waterSim[x,y].iteration != it) {
							waterSim[x,y].iteration = it;
							waterSim[x,y].volume = waterSim[x,y].newVolume;
					}	
				
					float volume = waterSim[x,y].volume;
					if (volume <=startingVolume*0.01){// // threshold for caring about cell
						continue;
					}
					
					float[] flows = {0.0f,0.0f,0.0f,0.0f};// store max flows in here
					float sum = 0.0f;
				
					for ( int n = 0; n < next; n++)// iterate over neighbours
					{
						if (waterSim[x+nextX[n],y+nextY[n]].inert ){// disabled cell
							continue;
						}
						if (waterSim[x+nextX[n],y+nextY[n]].iteration != it) {// update cell
							waterSim[x+nextX[n],y+nextY[n]].iteration= it;
							waterSim[x+nextX[n],y+nextY[n]].volume = waterSim[x+nextX[n],y+nextY[n]].newVolume;
						}
						// physics code 
						float neighbourHeight = m_ter.terrainData.GetHeight(x+nextX[n],y+nextY[n])/m_ter.terrainData.size.y;// 0,1
						float neighbourVolume = waterSim[x+nextX[n],y+nextY[n]].volume;
						//~0.001
						float flow = 0.5f*(height+volume - (neighbourHeight+neighbourVolume));
					
						//if (x==128 && y==128){
						//	Debug.Log (flow);
						//}

						if (flow <=0.0f){
							flow = 0.0f;
						}
						flows[n] = flow*damping;
						sum+=flows[n];

					}
					// check we have enough water in cell to flow to neighbours, 
					// if not scale it by volume of cell
					float factor = 1.0f;
					if (sum>volume){
						factor = volume/sum;
					}
					for ( int n = 0; n < next; n++)// iterate over neighbours
					{
						waterSim[x,y].newVolume+=-flows[n]*factor;
						waterSim[x+nextX[n],y+nextY[n]].newVolume+=+flows[n]*factor;
					}
					
				}
			}
		//}
		
		
		if (it%10 ==0){// only update every 10th iter
		for (int x = 1; x < m_ter.terrainData.heightmapWidth-1.0f; x++)
		{
		    for (int y = 1; y < m_ter.terrainData.heightmapHeight-1.0f; y++)
			{	
				waterlevel[y,x] = (m_ter.terrainData.GetHeight(x,y)/m_ter.terrainData.size.y)+(waterSim[x,y].newVolume);
			}
		}
		
        m_err.terrainData.SetHeights(0, 0, waterlevel);
		}
        //m_ter.terrainData.SetHeights(0, 0, heights);
		
	}
}
	
	
	